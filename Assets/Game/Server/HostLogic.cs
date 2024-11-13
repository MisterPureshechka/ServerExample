using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UniRx;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;
using static Game.Host.ServerEntity;

namespace Game.Server
{
    public class HostLogic : IDisposable
    {
        public struct Ctx
        {
            public ushort Port;

            public IReactiveCommand<float> OnUpdate;
        }

        private Dictionary<int, Dictionary<MessageType, string>> _data;

        private NetworkDriver _serverDriver;
        private NativeList<NetworkConnection> _connections;

        private Ctx _ctx;

        private readonly Stack<IDisposable> _disposables;

        public HostLogic(Ctx ctx)
        {
            _disposables = new ();

            _ctx = ctx;

            _serverDriver = NetworkDriver.Create();
            _disposables.Push(_serverDriver);

            var endPoint = NetworkEndPoint.AnyIpv4.WithPort(_ctx.Port);

            if (_serverDriver.Bind(endPoint) != 0)
                Debug.LogError($"\nFailed to bind to {endPoint}");
            else
                _serverDriver.Listen();

            _connections = new NativeList<NetworkConnection>(64, Allocator.Persistent);
            _disposables.Push(_connections);

            var onUpdateDisposable = _ctx.OnUpdate.Subscribe(OnServerUpdate);
            _disposables.Push(onUpdateDisposable);
        }

        private void OnServerUpdate(float deltaTime)
        {
            _serverDriver.ScheduleUpdate().Complete();
            for (int i = 0; i < _connections.Length; i++)
            {
                if (!_connections[i].IsCreated)
                {
                    _connections.RemoveAtSwapBack(i);
                    --i;
                }
            }

            NetworkConnection newConnection;
            while ((newConnection = _serverDriver.Accept()) != default)
            {
                _connections.Add(newConnection);
            }

            for (int i = 0; i < _connections.Length; i++)
            {
                if (!_connections[i].IsCreated)
                    continue;

                NetworkEvent.Type cmd;
                while ((cmd = _serverDriver.PopEventForConnection(_connections[i], out DataStreamReader stream)) != NetworkEvent.Type.Empty)
                {
                    if (cmd == NetworkEvent.Type.Data)
                    {
                        var rawData = stream.ReadFixedString4096();
                        var data = rawData.ToString();
                        var datas = data.Split("_");
                        var extraData = datas.Length == 1 ? null : datas[1];
                        if (Enum.TryParse<MessageType>(datas[0], out var dataType))
                        {
                            OnServerGetData(dataType, _connections[i], extraData);
                        }
                    }
                    else if (cmd == NetworkEvent.Type.Disconnect)
                    {
                        _connections[i] = default;
                    }
                }
            }
        }

        private void OnServerGetData(MessageType data, NetworkConnection connection, string extraData)
        {
            _data ??= new();
            if (!_data.ContainsKey(connection.InternalId))
                _data.Add(connection.InternalId, new ());
            _data[connection.InternalId][MessageType.LastUpdateTime] = DateTime.UtcNow.ToBinary().ToString();
            _data[connection.InternalId][data] = extraData;

            //remove old _data here...

            var response = JsonConvert.SerializeObject(_data);

            var result = _serverDriver.BeginSend(NetworkPipeline.Null, connection, out var writerGetData);
            if (result != 0)
            {
                Debug.LogWarning($"Something went wrong with {data} : {result}");
                return;
            }
            writerGetData.WriteFixedString4096(response);
            _serverDriver.EndSend(writerGetData);
        }

        public void Dispose()
        {
            while (_disposables.TryPop(out var disposable))
                disposable.Dispose();
        }
    }
}

