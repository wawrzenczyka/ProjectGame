﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace GameLib
{
    public class GameMasterState
    {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public bool GameEnded = false;
        public Team? Winner = null;
        private readonly double validPieceProbability;
        private readonly int maxPiecesOnBoard;
        private readonly GameRules gameRules;
        public int UndiscoveredRedGoalsLeft;
        public int UndiscoveredBlueGoalsLeft;
        private readonly Dictionary<(int senderId, int targetId), (object data, string senderMessageId)?> CommunicationData =
            new Dictionary<(int senderId, int targetId), (object data, string senderMessageId)?>();

        public readonly GameMasterBoard Board;
        public readonly Dictionary<int, PlayerState> PlayerStates = new Dictionary<int, PlayerState>();

        public GameMasterState(GameRules rules)
        {
            gameRules = rules;
            Board = new GameMasterBoard(gameRules);
            validPieceProbability = 1 - rules.BadPieceProbability;
            maxPiecesOnBoard = rules.MaxPiecesOnBoard;
            UndiscoveredRedGoalsLeft = rules.GoalCount;
            UndiscoveredBlueGoalsLeft = rules.GoalCount;
        }

        public void InitializePlayerPositions(int width, int height, int teamSize)
        {
            List<PlayerState> reds = new List<PlayerState>(PlayerStates.Values.Where(player => player.Team == Team.Red));
            List<PlayerState> blues = new List<PlayerState>(PlayerStates.Values.Where(player => player.Team == Team.Blue));

            for (int i = 0; i < teamSize; i++)
            {
                PlayerState redPlayer = reds[i];
                PlayerState bluePlayer = blues[i];
                int rowBlue = i / width; //top
                int rowRed = height - 1 - i / width; //bottom
                int columnRed = width / 2 + Distance(i % width) * Side(i); //from center, outwards
                int columnBlue = (width - 1) / 2 + Distance(i % width) * Side(i + 1);
                redPlayer.Position = (columnRed, rowRed);
                bluePlayer.Position = (columnBlue, rowBlue);
            }

            int Distance(int n)
            {
                return n % 2 == 0 ? n / 2 : n / 2 + 1;
            }

            int Side(int n)
            {
                return n % 2 == 0 ? 1 : -1;
            }
        }

        public Dictionary<int, AgentGameRules> GetAgentGameRules()
        {
            Dictionary<int, AgentGameRules> rules = new Dictionary<int, AgentGameRules>();

            foreach (var (id, playerState) in PlayerStates)
            {
                int[] teamIds = PlayerStates.Where(pair => pair.Value.Team == playerState.Team).Select(pair => pair.Key).ToArray();
                int leaderId = PlayerStates.Single(pair => pair.Value.IsLeader && pair.Value.Team == playerState.Team).Key;
                var privateRules = new AgentGameRules(gameRules, playerState.Position.X, playerState.Position.Y, teamIds, leaderId);
                rules.Add(id, privateRules);
            }

            return rules;
        }

        public void DelayPlayer(int playerId, int delayMultiplier)
        {
            var player = PlayerStates[playerId];
            logger.Debug($"Delaying agent {playerId} by {(delayMultiplier * gameRules.BaseTimePenalty)}ms");
            player.LastRequestTimestamp = DateTime.UtcNow;
            player.LastActionDelay = delayMultiplier * gameRules.BaseTimePenalty;
            PlayerStates[playerId] = player;
        }

        public void GeneratePiece()
        {
            if (Board.PieceCount < maxPiecesOnBoard)
            {
                Board.GeneratePiece(validPieceProbability);
                logger.Trace("Piece placed");
            }
            else
                logger.Debug("Max pieces on board reached");
        }

        public void GeneratePieceAt(int x, int y)
        {
            if (Board.PieceCount < maxPiecesOnBoard)
                Board.GeneratePieceAt(x, y, validPieceProbability);
        }

        public int Move(int playerId, MoveDirection direction)
        {
            var player = PlayerStates[playerId];

            CheckEligibility(player);

            var newPosition = player.Position;

            switch (direction)
            {
                case MoveDirection.Left:
                    newPosition.X--;
                    break;

                case MoveDirection.Right:
                    newPosition.X++;
                    break;

                case MoveDirection.Up:
                    newPosition.Y++;
                    break;

                case MoveDirection.Down:
                    newPosition.Y--;
                    break;
            }

            if (!IsOnBoard(newPosition))
                throw new OutOfBoardMoveException("Agent tried to move out of board!");

            var enemyTeam = player.Team == Team.Blue ? Team.Red : Team.Blue;
            if (Board.IsAgentInGoalArea(newPosition.X, newPosition.Y, enemyTeam))
                throw new OutOfBoardMoveException("Agent tried to move onto enemy goal area!");

            if (IsAnyAgentOn(newPosition))
                throw new AgentCollisionMoveException("Agent tried to on the space occupied by another agent!");

            player.Position = newPosition;
            PlayerStates[playerId] = player;

            DelayPlayer(playerId, gameRules.MoveMultiplier);

            return Board[newPosition.X, newPosition.Y].Distance;
        }

        private static void CheckEligibility(PlayerState player)
        {
            if (player.PendingLeaderCommunication)
                throw new PendingLeaderCommunicationException();
            if (!player.IsEligibleForAction)
                throw new DelayException();
        }

        private bool IsOnBoard((int, int) newPosition)
        {
            (int x, int y) = newPosition;
            return y < Board.Height && y >= 0 &&
                x < Board.Width && x >= 0;
        }

        private bool IsAnyAgentOn((int X, int Y) newPosition)
        {
            foreach ((var id, var playerState) in PlayerStates)
            {
                if (playerState.Position == newPosition)
                {
                    return true;
                }
            }
            return false;
        }

        public void PickUpPiece(int playerId)
        {
            PlayerState player = PlayerStates[playerId];

            CheckEligibility(player);

            if (player.Piece != null)
                throw new PieceOperationException("Cannot pick up piece if you already have one!");

            (int x, int y) = player.Position;

            if (!Board[x, y].HasPiece)
                throw new PieceOperationException("No piece on this field!");

            player.Piece = Board[x, y].Piece;
            Board.PiecesPositions.Remove((x, y));
            Board.BoardTable[x, y].Piece = null;

            Board.RecalculateDistances();
            PlayerStates[playerId] = player;

            DelayPlayer(playerId, gameRules.PickUpPieceMultiplier);
        }

        public PutPieceResult PutPiece(int playerId)
        {
            PlayerState player = PlayerStates[playerId];

            CheckEligibility(player);

            if (player.Piece == null)
                throw new PieceOperationException("Player doesn't have a piece!");

            (int x, int y) = player.Position;

            if (Board[x, y].HasPiece)
                throw new PieceOperationException("Cannot put another piece on this field!");

            DelayPlayer(playerId, gameRules.PutPieceMultiplier);

            if (Board.IsAgentInGoalArea(x, y, player.Team))
            {
                return PutPieceInGoalArea(playerId, x, y);
            }
            else
            {
                PutPieceInTaskArea(playerId, x, y);
                return PutPieceResult.PieceInTaskArea;
            }
        }

        private PutPieceResult PutPieceInGoalArea(int playerId, int x, int y)
        {
            PlayerState player = PlayerStates[playerId];
            PutPieceResult result = PutPieceResult.PieceWasFake;
            if (player.Piece.IsValid)
            {
                if (Board[x, y].IsGoal)
                {
                    result = PutPieceResult.PieceGoalRealized;
                    if (player.Team == Team.Red)
                    {
                        UndiscoveredRedGoalsLeft--;
                    }
                    else
                    {
                        UndiscoveredBlueGoalsLeft--;
                    }
                    if (UndiscoveredRedGoalsLeft == 0 || UndiscoveredBlueGoalsLeft == 0)
                    {
                        GameEnded = true;
                        Winner = UndiscoveredBlueGoalsLeft == 0 ? Team.Blue : Team.Red;
                    }
                    GameMasterField discoveredGoal = Board[x, y];
                    discoveredGoal.IsGoal = false;
                    Board[x, y] = discoveredGoal;
                }
                else
                {
                    result = PutPieceResult.PieceGoalUnrealized;
                }
            }
            DestroyPlayersPiece(playerId);
            return result;
        }

        private void PutPieceInTaskArea(int playerId, int x, int y)
        {
            PlayerState player = PlayerStates[playerId];
            Board.BoardTable[x, y].Piece = PlayerStates[playerId].Piece;
            Board.PiecesPositions.Add((x, y));
            player.Piece = null;
            PlayerStates[playerId] = player;

            Board.RecalculateDistances();
        }

        private void DestroyPlayersPiece(int playerId)
        {
            PlayerState player = PlayerStates[playerId];
            player.Piece = null;
            PlayerStates[playerId] = player;
            Board.PieceCount--;
        }

        public void DestroyPiece(int playerId)
        {
            PlayerState player = PlayerStates[playerId];

            CheckEligibility(player);

            if (player.Piece == null)
                throw new PieceOperationException("Player doesn't have a piece!");

            DestroyPlayersPiece(playerId);

            DelayPlayer(playerId, gameRules.DestroyPieceMultiplier);
        }

        public bool CheckPiece(int playerId)
        {
            PlayerState player = PlayerStates[playerId];

            CheckEligibility(player);

            if (player.Piece == null)
                throw new PieceOperationException("Player doesn't have a piece!");

            DelayPlayer(playerId, gameRules.CheckPieceMultiplier);

            return player.Piece.IsValid;
        }

        public DiscoveryResult Discover(int playerId)
        {
            PlayerState player = PlayerStates[playerId];

            CheckEligibility(player);

            DelayPlayer(playerId, gameRules.DiscoverMultiplier);

            int[,] array = Board.GetDistancesAround(player.Position.X, player.Position.Y);
            List<(int x, int y, int distance)> fields = new List<(int x, int y, int distance)>();
            for (int i = 0; i < 3; ++i)
            {
                for (int j = 0; j < 3; ++j)
                {
                    if (array[i, j] != int.MaxValue)
                    {
                        fields.Add((player.Position.X + i - 1, player.Position.Y + j - 1, array[i, j]));
                    }
                }
            }
            logger.Debug($"Discovery result for {playerId}: {(fields.Aggregate("", (s, tuple) => s + $"({tuple.x},{tuple.y},{tuple.distance}) "))}");
            return new DiscoveryResult(fields);
        }

        //Communication scheme:
        //agent -> gm (gm saves data to the dictionary) //SaveCommunicationData
        //gm -> ag2 (generic message, doesn't depend on gm state)
        //ag2 -> gm (DelayException if target is delayed, adds delay to the sender (doesn't check)) //(GetCommunicationData to check if the communication exists) + DelayCommunicationPartners
        //gm -> ag2 (gm reads data from the dictionary) //sends data obtained from GetCommunicationData
        //gm -> ag1 (forwarding data, doesn't depend on gm state)

        public void DelayCommunicationPartners(int senderId, int targetId)
        {
            PlayerState senderPlayer = PlayerStates[senderId];

            if (senderPlayer.IsLeader)
            {
                logger.Debug($"Agent {targetId} no longer has pending leader communication!");
                PlayerStates[targetId].PendingLeaderCommunication = false;
            }

            AddDelay(targetId, gameRules.CommunicationMultiplier);
            AddDelay(senderId, gameRules.CommunicationMultiplier);
        }

        private void AddDelay(int playerId, int delayMultiplier)
        {
            var player = PlayerStates[playerId];
            int lastPenaltyDurationLeftTime = Math.Max(0, player.LastActionDelay - (int)(DateTime.UtcNow - player.LastRequestTimestamp).TotalMilliseconds);
            player.LastActionDelay = delayMultiplier * gameRules.BaseTimePenalty + lastPenaltyDurationLeftTime;
            player.LastRequestTimestamp = DateTime.UtcNow;
            logger.Debug($"Adding delay for agent {playerId}, now it's {player.LastActionDelay}ms");
            PlayerStates[playerId] = player;
        }

        public void SaveCommunicationData(int senderId, int targetId, object data, string senderMessageId)
        {
            PlayerState senderPlayer = PlayerStates[senderId];

            CheckEligibility(senderPlayer);

            if (CommunicationData.ContainsKey((senderId, targetId)) && CommunicationData[(senderId, targetId)] != null)
            {
                throw new CommunicationInProgressException($"Agent {senderId} waits for response from {targetId}");
            }

            if (senderPlayer.IsLeader)
            {
                PlayerStates[targetId].PendingLeaderCommunication = true;
                logger.Debug($"Agent {targetId} has now pending leader communication!");
            }

            CommunicationData[(senderId, targetId)] = (data, senderMessageId);
        }

        public (object data, string senderMessageId) GetCommunicationData(int senderId, int targetId)
        {
            if (CommunicationData.TryGetValue((senderId, targetId), out (object data, string senderMessageId)? responseData))
            {
                if (!responseData.HasValue)
                    throw new CommunicationException($"Communication data for pair ({senderId}, {targetId}) doesn't exist!");

                CommunicationData[(senderId, targetId)] = null;
                return responseData.Value;
            }
            else
            {
                throw new CommunicationException($"Communication data for pair ({senderId}, {targetId}) doesn't exist!");
            }
        }

        public void VerifyLeaderCommunicationState(int senderId, int targetId, bool agreement)
        {
            var senderPlayer = PlayerStates[senderId];
            var targetPlayer = PlayerStates[targetId];

            if (targetPlayer.PendingLeaderCommunication && !senderPlayer.IsLeader)
                throw new PendingLeaderCommunicationException();

            if (targetPlayer.PendingLeaderCommunication && senderPlayer.IsLeader && !agreement)
                throw new PendingLeaderCommunicationException();
        }

        public void JoinGame(int agentId, int teamId, bool wantToBeLeader)
        {
            if (PlayerStates.ContainsKey(agentId))
                throw new GameSetupException($"Agent with Id {agentId} is already connected.");

            if (teamId != 0 && teamId != 1)
                throw new GameSetupException($"No team with Id {teamId}");

            Team team = (Team)teamId;

            int teamMembers = PlayerStates.Count(p => p.Value.Team == team);

            if (teamMembers >= gameRules.TeamSize)
                throw new GameSetupException($"Team ${teamId} is full");

            bool isLeaderInTeam = PlayerStates.Any(p => p.Value.IsLeader && p.Value.Team == team);

            bool willBeLeader;
            if (isLeaderInTeam)
            {
                willBeLeader = false;
            }
            else if (teamMembers == gameRules.TeamSize - 1)
            {
                willBeLeader = true;
            }
            else
            {
                willBeLeader = wantToBeLeader;
            }

            PlayerStates.Add(agentId, new PlayerState(-1, -1, team, willBeLeader));
        }
    }
}