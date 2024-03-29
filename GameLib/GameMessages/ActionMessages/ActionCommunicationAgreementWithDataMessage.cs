﻿using System;

namespace GameLib
{
    internal class ActionCommunicationAgreementWithDataMessage : ActionMessage
    {
        private readonly object data;

        public int SenderId { get; }
        public bool AcceptsCommunication { get; }

        public ActionCommunicationAgreementWithDataMessage(int agentId, int senderId, bool acceptsCommunication, object data, string messageId) : base(agentId, messageId)
        {
            SenderId = senderId;
            AcceptsCommunication = acceptsCommunication;
            this.data = data;
        }

        public override void Handle(GameMaster gameMaster)
        {
            gameMaster.CommunicationAgreementWithData(SenderId, AgentId, AcceptsCommunication, data, MessageId);
        }

        public override string ToString()
        {
            return $"ActionCommunicationAgreementWithDataMessage (agentId: {AgentId}, messageId: {MessageId}, senderId: {SenderId}, agreement status: {AcceptsCommunication})";
        }
    }
}