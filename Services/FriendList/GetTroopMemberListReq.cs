﻿using Konata.Core.Attributes;
using Konata.Core.Events;
using Konata.Core.Events.Model;
using Konata.Core.Packets;
using Konata.Core.Packets.SvcRequest;
using Konata.Core.Packets.SvcResponse;

// ReSharper disable UnusedType.Global

namespace Konata.Core.Services.Friendlist
{
    [EventSubscribe(typeof(PullGroupMemberListEvent))]
    [Service("friendlist.GetTroopMemberListReq", "Pull group member list")]
    public class GetTroopMemberListReq : IService
    {
        public bool Parse(SSOFrame input, BotKeyStore keystore, out ProtocolEvent output)
        {
            var response = new SvcRspGetTroopMemberListResp(input.Payload.GetBytes());

            output = PullGroupMemberListEvent.Result(response.Result, response.ErrorCode,
                response.GroupUin, response.GroupCode, response.Members, response.NextUin);
            
            return true;
        }

        public bool Build(Sequence sequence, PullGroupMemberListEvent input,
            BotKeyStore keystore, BotDevice device, out int newSequence, out byte[] output)
        {
            output = null;
            newSequence = sequence.NewSequence;

            var svcRequest = new SvcReqGetTroopMemberListReq
                (input.SelfUin, input.GroupUin, input.GroupCode, input.NextUin);

            if (SSOFrame.Create("friendlist.GetTroopMemberListReq", PacketType.TypeB,
                newSequence, sequence.Session, svcRequest, out var ssoFrame))
            {
                if (ServiceMessage.Create(ssoFrame, AuthFlag.D2Authentication,
                    keystore.Account.Uin, keystore.Session.D2Token, keystore.Session.D2Key, out var toService))
                {
                    return ServiceMessage.Build(toService, device, out output);
                }
            }

            return false;
        }

        public bool Build(Sequence sequence, ProtocolEvent input,
            BotKeyStore keystore, BotDevice device, out int newSequence, out byte[] output)
            => Build(sequence, (PullGroupMemberListEvent) input, keystore, device, out newSequence, out output);
    }
}