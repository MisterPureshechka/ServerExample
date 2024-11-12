using Game.Shared;
using Newtonsoft.Json;
using System;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;

namespace Game.Host
{
    public class HostEntryPoint : MonoBehaviour
    {
        [SerializeField] private ushort _port = 7000;

        private NetworkDriver _driver;
        private NativeList<NetworkConnection> _connections;

        private void OnEnable()
        {
            _driver = NetworkDriver.Create();

            var endPoint = NetworkEndPoint.AnyIpv4.WithPort(_port);

            if (_driver.Bind(endPoint) != 0)
                Debug.LogError($"\nFailed to bind to {endPoint}");
            else
                _driver.Listen();

            _connections = new NativeList<NetworkConnection>(64, Allocator.Persistent);
        }

        private void OnDisable()
        {
            if (_driver.IsCreated)
            {
                _driver.Dispose();
                _connections.Dispose();
            }
        }

        private void Update()
        {
            _driver.ScheduleUpdate().Complete();
            for (int i = 0; i < _connections.Length; i++)
            {
                if (!_connections[i].IsCreated)
                {
                    _connections.RemoveAtSwapBack(i);
                    --i;
                }
            }

            NetworkConnection newConnection;
            while ((newConnection = _driver.Accept()) != default)
            {
                _connections.Add(newConnection);
            }

            for (int i = 0; i < _connections.Length; i++)
            {
                if (!_connections[i].IsCreated)
                    continue;

                NetworkEvent.Type cmd;
                while ((cmd = _driver.PopEventForConnection(_connections[i], out DataStreamReader stream)) != NetworkEvent.Type.Empty)
                {
                    if (cmd == NetworkEvent.Type.Data)
                    {
                        var rawData = stream.ReadFixedString4096();
                        var data = rawData.ToString();
                        var datas = data.Split("_");
                        var extraData = datas.Length == 1 ? null : datas[1];
                        if (Enum.TryParse<MessageType>(datas[0], out var dataType))
                        {
                            OnGetData(dataType, _connections[i], extraData);
                        }
                    }
                    else if (cmd == NetworkEvent.Type.Disconnect)
                    {
                        _connections[i] = default;
                    }
                }
            }
        }

        private void OnGetData(MessageType data, NetworkConnection connection, string extraData)
        {
            string response;
            switch (data)
            {
                case MessageType.Ping:
                    var sendedDataPing = new SendedData
                    {
                        DataType = data,
                        SerializedData = "Ping",
                    };
                    response = JsonConvert.SerializeObject(sendedDataPing);
                    break;
                default:
                    return;
            }
            Send(data, connection, response);
        }

        private void Send(MessageType messageType, NetworkConnection connection, string response)
        {
            var result = _driver.BeginSend(NetworkPipeline.Null, connection, out var writerGetData);
            if (result != 0)
            {
                Debug.LogWarning($"Something went wrong with {messageType} : {result}");
                return;
            }
            writerGetData.WriteFixedString4096(response);
            _driver.EndSend(writerGetData);
        }
    }
}

