using Game.Shared;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Unity.Networking.Transport;
using UnityEngine;

namespace Game.Client
{
    public class ClientEntryPoint : MonoBehaviour
    {
        [SerializeField] private string _ip;
        [SerializeField] private ushort _port;

        private NetworkDriver _driver;
        private NetworkConnection _connection;

        private void OnEnable()
        {
            _driver = NetworkDriver.Create();
            _connection = default;
        }

        private void OnDisable()
        {
            _connection.Disconnect(_driver);
            _connection = default;
            _driver.Dispose();
        }

        private void Update()
        {
            if (Input.GetKeyUp(KeyCode.Space))
                Send(MessageType.Ping);

            _driver.ScheduleUpdate().Complete();
            if (!_connection.IsCreated) return;

            NetworkEvent.Type cmd;
            while ((cmd = _connection.PopEvent(_driver, out DataStreamReader stream)) != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Data)
                {
                    var rawData = stream.ReadFixedString4096();
                    var data = rawData.ToString();
                    var receivedData = JsonConvert.DeserializeObject<SendedData>(data);
                    OnGetData(receivedData);
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    _connection = default;
                }
            }
        }

        private void OnGetData(SendedData receivedData)
        {
            Debug.Log($"Got the value {receivedData.DataType} back from the server\n{receivedData.SerializedData}");
            switch (receivedData.DataType)
            {
                case MessageType.Ping:

                    break;
                default:
                    return;
            }
        }

        private async void Send(MessageType message, string extraData = null)
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
                var sendMessage = message.ToString();
                if (extraData != null)
                    sendMessage += $"_{extraData}";
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
            if (!NetworkEndPoint.TryParse(_ip, _port, out NetworkEndPoint endPoint))
            {
                Debug.LogError($"\nFailed to parse {nameof(NetworkEndPoint)}");
                return false;
            }

            _connection = _driver.Connect(endPoint);
            return true;
        }
    }
}
