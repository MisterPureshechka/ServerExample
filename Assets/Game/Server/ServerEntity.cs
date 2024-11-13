using Game.Server;
using System;
using System.Collections.Generic;
using UniRx;

namespace Game.Host
{
    public class ServerEntity : IDisposable
    {
        public enum MessageType : byte
        {
            //auto
            LastUpdateTime,

            //manual
            Ping,
        }

        public struct Ctx
        {
            public string Ip;
            public ushort Port;

            public IReactiveCommand<float> OnUpdate;

            public IReactiveCommand<(MessageType message, string extraData)> SendData;
            public IReactiveCommand<Dictionary<int, Dictionary<MessageType, string>>> ReceiveData;
        }

        private readonly Stack<IDisposable> _disposables;

        public ServerEntity(Ctx ctx)
        {
            _disposables = new();

            var host = new HostLogic(new HostLogic.Ctx 
            {
                Port = ctx.Port,
                OnUpdate = ctx.OnUpdate,
            });
            _disposables.Push(host);

            var client = new ClientLogic(new ClientLogic.Ctx
            {
                Ip = ctx.Ip,
                Port = ctx.Port,

                OnUpdate = ctx.OnUpdate,

                SendData = ctx.SendData,
                ReceiveData = ctx.ReceiveData,
            });
            _disposables.Push(client);
        }

        public void Dispose()
        {
            while (_disposables.TryPop(out var disposable))
                disposable.Dispose();
        }
    }
}

