using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UniRx;
using Unity.Networking.Transport;
using UnityEngine;
using static Game.Host.ServerEntity;

namespace Game.Server
{
    public class ClientLogic : IDisposable
    {
        public struct Ctx
        {
            public string Ip;
            public ushort Port;

            public IReactiveCommand<float> OnUpdate;

            public IReactiveCommand<(MessageType message, string extraData)> SendData;
            public IReactiveCommand<Dictionary<int, Dictionary<MessageType, string>>> ReceiveData;
        }

        private NetworkDriver _driver;
        private NetworkConnection _connection;

        private Ctx _ctx;

        private readonly Stack<IDisposable> _disposables;

        public ClientLogic(Ctx ctx)
        {
            _disposables = new();

            _ctx = ctx;

            _driver = NetworkDriver.Create();
            _disposables.Push(_driver);

            _connection = default;

            var onUpdateDisposable = _ctx.OnUpdate.Subscribe(OnUpdate);
            _disposables.Push(onUpdateDisposable);

            var subscribeDisposable = _ctx.SendData.Subscribe(Send);
            _disposables.Push(subscribeDisposable);
        }

        private void OnUpdate(float deltaTime)
        {
            _driver.ScheduleUpdate().Complete();
            if (!_connection.IsCreated) return;

            NetworkEvent.Type cmd;
            while ((cmd = _connection.PopEvent(_driver, out DataStreamReader stream)) != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Data)
                {
                    var rawData = stream.ReadFixedString4096();
                    var data = rawData.ToString();
                    var receivedData = JsonConvert.DeserializeObject<Dictionary<int, Dictionary<MessageType, string>>>(data);

                    _ctx.ReceiveData.Execute(receivedData);
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    _connection = default;
                }
            }
        }

        private async void Send((MessageType message, string extraData) data)
        {
            try
            {
                NetworkConnection.State connectionState;
                while ((connectionState = _driver.GetConnectionState(_connection)) != NetworkConnection.State.Connected)
                {
                    if (connectionState == NetworkConnection.State.Disconnected)
                        TryConnect();
                    await Task.Delay(100);
                }

                _driver.BeginSend(_connection, out var writer);
                var sendMessage = data.message.ToString();
                if (data.extraData != null)
                    sendMessage += $"_{data.extraData}";
                writer.WriteFixedString4096(sendMessage);
                _driver.EndSend(writer);
            }
            catch
            {
                // ignored
            }
        }

        private bool TryConnect()
        {
            if (!NetworkEndPoint.TryParse(_ctx.Ip, _ctx.Port, out NetworkEndPoint endPoint))
            {
                Debug.LogError($"\nFailed to parse {nameof(NetworkEndPoint)}");
                return false;
            }

            _connection = _driver.Connect(endPoint);
            return true;
        }

        public void Dispose()
        {
            while (_disposables.TryPop(out var disposable))
                disposable.Dispose();
        }
    }
}

