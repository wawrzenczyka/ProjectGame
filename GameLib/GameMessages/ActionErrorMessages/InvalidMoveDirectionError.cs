﻿using GameLib.GameMessages;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameLib.Actions
{
    class InvalidMoveDirectionError : ActionErrorMessage
    {

        public InvalidMoveDirectionError(int agentId, int timestamp, string messageId) : base(agentId, timestamp, messageId)
        {
        }

        public override void Handle(Agent agent)
        {
            agent.HandleInvalidMoveDirectionError(Timestamp, MessageId);
        }
    }
}
