using Game.Host;
using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;
using static Game.Host.ServerEntity;

namespace Game
{
    public class GameEntryPoint : MonoBehaviour
    {
        [SerializeField]
        private string _ip = "127.0.0.1";
        [SerializeField]
        private ushort _port = 7000;

        private ReactiveCommand<float> _onUpdate;

        private ReactiveCommand<(MessageType message, string extraData)> _sendData;
        private ReactiveCommand<Dictionary<int, SendedData>> _receiveData;


        private readonly Stack<IDisposable> _disposables = new ();

        private void OnEnable()
        {
            _onUpdate = new ();
            _disposables.Push(_onUpdate);

            _sendData = new ();
            _disposables.Push(_sendData);

            _receiveData = new ();
            _disposables.Push(_receiveData);

            var receivedDataDisposable = _receiveData.Subscribe(d => Debug.Log($"{string.Join(",\n", d.Select(p => $"{p.Key} | {p.Value.DataType}"))}"));
            _disposables.Push(receivedDataDisposable);

            var serverEntity = new ServerEntity(new ServerEntity.Ctx
            {
                Ip = _ip,
                Port = _port,

                OnUpdate = _onUpdate,

                SendData = _sendData,
                ReceiveData = _receiveData,

            });
            _disposables.Push(serverEntity);
        }

        private void Update()
        {
            _onUpdate.Execute(Time.deltaTime);

            if (Input.GetKeyUp(KeyCode.Space))
                _sendData.Execute((MessageType.Ping, "dsadsad"));
        }

        public void OnDisable()
        {
            while (_disposables.TryPop(out var disposable))
                disposable.Dispose();
        }
    }
}
