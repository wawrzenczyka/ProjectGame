﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace GameLib
{
    public class InteractiveDecisionModule : DecisionModuleBase
    {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private bool registered = false;

        public async override Task<Action> ChooseAction(int agentId, AgentState agentState)
        {
            if (!registered)
            {
                InteractiveInputProvider.Register(agentId);
                registered = true;
            }

            ConsoleKey key = await InteractiveInputProvider.GetKey(agentId);

            Action action = ParseInput(key, agentId, agentState);
            Console.WriteLine(action);

            return action;
        }
        
        private Action ParseInput(ConsoleKey key, int agentId, AgentState agentState)
        {
            Action action;

            Random random = RandomGenerator.GetGenerator(); 

            switch (key)
            {
                case ConsoleKey.UpArrow:
                    action = new ActionMove(MoveDirection.Up);
                    logger.Debug($"Agent {agentId} chose action ActionMove with direction {MoveDirection.Up}");
                    break;
                case ConsoleKey.DownArrow:
                    action = new ActionMove(MoveDirection.Down);
                    logger.Debug($"Agent {agentId} chose action ActionMove with direction {MoveDirection.Down}");
                    break;
                case ConsoleKey.LeftArrow:
                    action = new ActionMove(MoveDirection.Left);
                    logger.Debug($"Agent {agentId} chose action ActionMove with direction {MoveDirection.Left}");
                    break;
                case ConsoleKey.RightArrow:
                    action = new ActionMove(MoveDirection.Right);
                    logger.Debug($"Agent {agentId} chose action ActionMove with direction {MoveDirection.Right}");
                    break;
                case ConsoleKey.Q:
                    action = new ActionPickPiece();
                    logger.Debug($"Agent {agentId} chose action ActionPickPiece");
                    break;
                case ConsoleKey.W:
                    action = new ActionCheckPiece();
                    logger.Debug($"Agent {agentId} chose action ActionCheckPieceMessage");
                    break;
                case ConsoleKey.E:
                    action = new ActionPutPiece();
                    logger.Debug($"Agent {agentId} chose action ActionPutPiece");
                    break;
                case ConsoleKey.R:
                    action = new ActionDestroyPiece();
                    logger.Debug($"Agent {agentId} chose action ActionDestroyPiece");
                    break;
                case ConsoleKey.A:
                    var teammate = agentState.TeamIds[random.Next(agentState.TeamIds.Length)];
                    var requestData = DataProcessor.CreateCommunicationDataForCommunicationWith(teammate, agentState);
                    action = new ActionCommunicate(teammate, requestData);
                    logger.Debug($"Agent {agentId} chose action ActionCommunicationRequestWithData with agent {teammate} with data {requestData}");
                    break;
                case ConsoleKey.S:
                    var randomTeammate = agentState.TeamIds[random.Next(agentState.TeamIds.Length)];
                    bool agreement = (random.Next(2) == 1) ? true : false;
                    var responseData = DataProcessor.CreateCommunicationDataForCommunicationWith(randomTeammate, agentState);
                    action = new ActionCommunicationAgreement(randomTeammate, agreement, responseData);
                    logger.Debug($"Agent {agentId} chose action ActionCommunicationAgreementWithData with agent {randomTeammate} with data {responseData} - he {(agreement ? "agrees" : "doesn't agree")} for the communication");
                    break;
                case ConsoleKey.D:
                    action = new ActionDiscovery();
                    logger.Debug($"Agent {agentId} chose action ActionDiscovery");
                    break;
                default:
                    logger.Error($"Agent {agentId} received invalid input");
                    throw new ArgumentException("Invalid input");
            }

            return action;
        }
    }
}
