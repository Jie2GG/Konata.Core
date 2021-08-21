﻿using System;

using Konata.Core.Events;
using Konata.Core.Events.Model;
using Konata.Core.Packets;
using Konata.Core.Attributes;

namespace Konata.Core.Services.LongConn
{
    [EventSubscribe(typeof(LongConnOffPicUpEvent))]

    [Service("LongConn.OffPicUp", "Image upload")]
    public class OffPicUp : IService
    {
        public bool Build(Sequence sequence, ProtocolEvent input,
            BotKeyStore signInfo, BotDevice device, out int newSequence, out byte[] output)
        {
            throw new NotImplementedException();
        }

        public bool Parse(SSOFrame input, BotKeyStore signInfo, out ProtocolEvent output)
        {
            throw new NotImplementedException();
        }
    }
}