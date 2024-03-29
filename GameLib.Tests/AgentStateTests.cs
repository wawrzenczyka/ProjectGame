﻿using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GameLib.Tests
{
    public class AgentStateTests
    {
        //factory methods - in order to abstract sometimes complex object creation
        private AgentGameRules GetDefaultRules()
        {
            return new AgentGameRules(boardWidth: 8, boardHeight: 8, goalAreaHeight: 2, agentStartX: 1, agentStartY: 1, teamSize: 2, agentIdsFromTeam: new int[] { 0, 1 }, leaderId: 0);
        }

        private AgentState GetState()
        {
            return new AgentState();
        }

        private AgentState GetSetUpState(AgentGameRules rules)
        {
            var state = new AgentState();
            state.Setup(rules);

            return state;
        }

        [Theory]
        [InlineData(4, 1)]
        [InlineData(7, 0)]
        public void Setup_WhenCalled_SetsAgentsPositions(int X, int Y)
        {
            var state = GetState();
            var rules = new AgentGameRules(boardWidth: 8, boardHeight: 8, agentStartX: X, agentStartY: Y, teamSize: 2, agentIdsFromTeam: new int[] { 0, 1 }, leaderId: 0);

            state.Setup(rules);

            state.Position.X.ShouldBe(X);
            state.Position.Y.ShouldBe(Y);
        }

        [Theory]
        [InlineData(8, 8, 2)]
        [InlineData(4, 6, 1)]
        public void Setup_WhenCalled_InitializesBoard(int width, int height, int goalAreaHeight)
        {
            var state = GetState();
            var rules = new AgentGameRules(boardWidth: width, boardHeight: height, goalAreaHeight: goalAreaHeight, agentStartX: 0, agentStartY: 0, teamSize: 2, agentIdsFromTeam: new int[] { 0, 1 }, leaderId: 0);

            state.Setup(rules);

            state.Board.ShouldNotBe(null);
            state.Board.Width.ShouldBe(width);
            state.Board.Height.ShouldBe(height);
            state.Board.GoalAreaHeight.ShouldBe(goalAreaHeight);
        }

        [Theory]
        [InlineData(MoveDirection.Up)]
        [InlineData(MoveDirection.Down)]
        [InlineData(MoveDirection.Left)]
        [InlineData(MoveDirection.Right)]
        public void Move_WhenCalled_ChangesAgentsPosition(MoveDirection direction)
        {
            var rules = GetDefaultRules();
            var state = GetSetUpState(rules);
            var distance = 1;

            state.Move(direction, distance, 0);

            var expectedX = rules.AgentStartX;
            var expectedY = rules.AgentStartY;
            switch (direction)
            {
                case MoveDirection.Left:
                    expectedX--;
                    break;

                case MoveDirection.Right:
                    expectedX++;
                    break;

                case MoveDirection.Up:
                    expectedY++;
                    break;

                case MoveDirection.Down:
                    expectedY--;
                    break;
            }
            state.Position.X.ShouldBe(expectedX);
            state.Position.Y.ShouldBe(expectedY);
        }

        [Theory]
        [InlineData(MoveDirection.Left)]
        [InlineData(MoveDirection.Down)]
        public void Move_WhenMoveGoesOutOfBoard_ThrowsOutOfBoardMoveException(MoveDirection direction)
        {
            var rules = new AgentGameRules(boardWidth: 8, boardHeight: 8, agentStartX: 0, agentStartY: 0, teamSize: 2, agentIdsFromTeam: new int[] { 0, 1 }, leaderId: 0);
            var state = GetSetUpState(rules);
            var distance = 1;

            Should.Throw<OutOfBoardMoveException>(() => state.Move(direction, distance, 0));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(0)]
        [InlineData(5)]
        public void MoveAgent_WhenCalled_UpdatesBoard(int distance)
        {
            var rules = GetDefaultRules();
            var state = GetSetUpState(rules);

            var callTime = 0;
            state.Move(MoveDirection.Down, distance, 1);

            AgentField field = state.Board[state.Position.X, state.Position.Y];
            field.Distance.ShouldBe(distance);
            field.Timestamp.ShouldBeGreaterThan(callTime);
        }

        [Fact]
        public void PickUpPiece_WhenAgentDoesntHavePiece_UpdatesPieceState()
        {
            var rules = GetDefaultRules();
            var state = GetSetUpState(rules);
            state.HoldsPiece = false;

            state.PickUpPiece(0);

            state.HoldsPiece.ShouldBe(true);
            state.PieceState.ShouldBe(PieceState.Unknown);
        }

        [Fact]
        public void PickUpPiece_WhenAgentDoesntHavePiece_SetsDistanceToMinusOne()
        {
            var rules = GetDefaultRules();
            var state = GetSetUpState(rules);
            int x = 1;
            int y = 1;
            state.Position = (x, y);
            state.Board.SetDistance(x, y, 5, 0);

            state.PickUpPiece(0);

            state.Board[x, y].Distance.ShouldBe(-1);
        }

        [Fact]
        public void PickUpPiece_WhenAgentHasPiece_ThrowsPieceOperationException()
        {
            var state = GetState();
            state.HoldsPiece = true;

            Should.Throw<PieceOperationException>(() => state.PickUpPiece(0), "Picking up piece when agent has one already");
        }

        [Theory]
        [InlineData(PieceState.Invalid)]
        [InlineData(PieceState.Valid)]
        public void SetPieceState_WhenCalled_UpdatesPieceState(PieceState newState)
        {
            var state = GetState();

            state.SetPieceState(newState);

            state.PieceState.ShouldBe(newState);
        }

        [Fact]
        public void PlacePiece_WhenAgentHasPiece_UpdatesPieceState()
        {
            var state = GetState();
            state.HoldsPiece = true;

            state.PlacePiece(PutPieceResult.PieceInTaskArea);

            state.HoldsPiece.ShouldBe(false);
            state.PieceState.ShouldBe(PieceState.Unknown);
        }

        [Fact]
        public void DestroyPiece_WhenAgentHasPiece_UpdatesPieceState()
        {
            var state = GetState();
            state.HoldsPiece = true;

            state.DestroyPiece();

            state.HoldsPiece.ShouldBe(false);
            state.PieceState.ShouldBe(PieceState.Unknown);
        }

        [Fact]
        public void PutPiece_WhenAgentDoesntHavePiece_ThrowsPieceOperationException()
        {
            var state = GetState();
            state.HoldsPiece = false;

            Should.Throw<PieceOperationException>(() => state.PlacePiece(PutPieceResult.PieceGoalRealized), "Placing piece when agent doesn't have it");
        }

        [Fact]
        public void DestroyPiece_WhenAgentDoesntHavePiece_ThrowsPieceOperationException()
        {
            var state = GetState();
            state.HoldsPiece = false;

            Should.Throw<PieceOperationException>(() => state.DestroyPiece(), "Destroying piece when agent doesn't have it");
        }

        [Fact]
        public void ApplyDiscoveryResult_WhenDiscoveryResultIsValid_UpdatesBoard()
        {
            var rules = GetDefaultRules();
            var state = GetSetUpState(rules);
            List<(int x, int y, int distance)> fields = new List<(int x, int y, int distance)>
            {(5,3,4),(5,4,3),(5,5,2),(4,3,3),(4,4,2),(4,5,1),(3,3,2),(3,4,1),(3,5,0)};
            var discoveryResult = new DiscoveryResult(fields);

            state.Discover(discoveryResult, 0);

            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    if (IsInBounds(state.Board, 4 + i, 4 + j))
                        state.Board[4 + i, 4 + j].Distance.ShouldBe(
                            fields.First(t => t.x == 4 + i && t.y == 4 + j).distance);
                }
            }
        }

        private bool IsInBounds(AgentBoard board, int x, int y)
        {
            return x >= 0 && x < board.Width &&
                y >= 0 && y < board.Height;
        }

        [Fact]
        public void UpdateBoardWithCommunicationData_WhenBoardsAreDifferentSizes_ThrowsInvalidDiscoveryResultException()
        {
            var rules = new AgentGameRules(boardWidth: 8, teamSize: 2, agentIdsFromTeam: new int[] { 0, 1 }, leaderId: 0);
            var state = GetSetUpState(rules);
            var resultRules = new AgentGameRules(boardWidth: 10, teamSize: 2, agentIdsFromTeam: new int[] { 0, 1 }, leaderId: 0);
            var resultBoard = new AgentBoard(resultRules);

            Should.Throw<InvalidCommunicationResultException>(() => state.UpdateBoardWithCommunicationData(resultBoard));
        }

        [Fact]
        public void UpdateBoardWithCommunicationData_WhenResultIsNewer_UpdatesBoard()
        {
            int distance = 1;
            var rules = GetDefaultRules();
            var state = GetSetUpState(rules);
            var resultBoard = new AgentBoard(rules);
            SetupCommunicationBoards(state.Board, resultBoard, int.MaxValue, distance);

            state.UpdateBoardWithCommunicationData(resultBoard);

            for (int x = 0; x < state.Board.Width; x++)
            {
                for (int y = 0; y < state.Board.Height; y++)
                {
                    state.Board[x, y].Distance.ShouldBe(distance);
                }
            }
        }

        [Fact]
        public void UpdateBoardWithCommunicationData_WhenResultIsOlder_DoesntUpdatesBoard()
        {
            int distance = 1;
            var rules = GetDefaultRules();
            var state = GetSetUpState(rules);
            var resultBoard = new AgentBoard(rules);
            SetupCommunicationBoards(state.Board, resultBoard, 0, distance);

            state.UpdateBoardWithCommunicationData(resultBoard);

            for (int x = 0; x < state.Board.Width; x++)
            {
                for (int y = 0; y < state.Board.Height; y++)
                {
                    state.Board[x, y].Distance.ShouldBe(-1);
                }
            }
        }

        private static void SetupCommunicationBoards(AgentBoard agentBoard, AgentBoard resultBoard, int value, int distance)
        {
            for (int x = 0; x < resultBoard.Width; x++)
            {
                for (int y = 0; y < resultBoard.Height; y++)
                {
                    agentBoard.BoardTable[x, y].SetDistance(-1, 1000);
                    resultBoard.BoardTable[x, y].SetDistance(distance, value);
                }
            }
        }
    }
}