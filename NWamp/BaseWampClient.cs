using System.Collections.Generic;
using System.Threading.Tasks;
using NWamp.Messages;
using System;
using NWamp.Transport;
using System.Linq;
using NWamp.Rpc;

namespace NWamp
{
    /// <summary>
    /// Abstract class representing WAMP client object.
    /// </summary>
    public abstract class BaseWampClient : IWampClient
    {
        /// <summary>
        /// Message provider used for message serialization and deserialization.
        /// </summary>
        protected IMessageProvider MessageProvider;

        /// <summary>
        /// List of currently subscribed topics.
        /// </summary>
        protected readonly HashSet<string> topics;

        /// <summary>
        /// Current client session identifier.
        /// </summary>
        protected string SessionId { get; set; }

        /// <summary>
        /// Current client session identifier.
        /// </summary>
        protected ITypeResolver TypeResolver { get; set; }

        protected Dictionary<string, Action<object>> _callbackMap;

        protected Dictionary<string, Action<Exception>> _errorMap;

        protected int _callbackID = 0;

        protected BaseWampClient(IMessageProvider messageProvider, ITypeResolver typeResolver)
        {
            MessageProvider = messageProvider;
            TypeResolver = typeResolver;
            this.topics = new HashSet<string>();
            _callbackMap = new Dictionary<string, Action<object>>();
            _errorMap = new Dictionary<string, Action<Exception>>();
        }

        public bool IsConnected { get; private set; }

        public IEnumerable<KeyValuePair<string, string>> Prefixes { get; private set; }

        public IWampConnection Connection { get; set; }
        public virtual void Connect(string address)
        {
            //has to set Connection
        }

        public void Prefix(string curie, string uri)
        {
            var msg = new PrefixMessage(curie, uri);
            var json = MessageProvider.SerializeMessage(msg);

            Connection.Send(json);
        }

        public void On<T>(string eventName, Action<T> action)
        {
            this.Subscribe(eventName);
            lock (_callbackMap)
            {
                _callbackMap[eventName] = (result) =>
                {
                    T resultObject = TypeResolver.Resolve<T>(result);
                    action(resultObject);
                };

            }

        }

        public void Subscribe(string topicUri)
        {
            var subMsg = new SubscribeMessage(topicUri);
            var json = MessageProvider.SerializeMessage(subMsg);
            Connection.Send(json);

            this.topics.Add(topicUri);
        }

        public void Unsubscribe(string topicUri)
        {
            var subMsg = new UnsubscribeMessage(topicUri);
            var json = MessageProvider.SerializeMessage(subMsg);
            Connection.Send(json);

            this.topics.Remove(topicUri);
        }

        public void Publish(string topicUri, object eventObject, bool excludeSelf = false)
        {
            var msg = new PublishMessage(topicUri, eventObject, excludeSelf);
            var json = MessageProvider.SerializeMessage(msg);
            Connection.Send(json);
        }

        public void PublishTo(string topicUri, object eventObject, IEnumerable<string> eligibleSessions)
        {
            var msg = new PublishMessage(topicUri, eventObject, null, eligibleSessions);
            var json = MessageProvider.SerializeMessage(msg);
            Connection.Send(json);
        }

        public void PublishExcept(string topicUri, object eventObject, IEnumerable<string> excludeSessions)
        {
            var msg = new PublishMessage(topicUri, eventObject, excludeSessions, null);
            var json = MessageProvider.SerializeMessage(msg);
            Connection.Send(json);
        }

        public Task<TResult> CallAsync<TResult>(string procUri, params object[] arguments)
        {
            var tcs = new TaskCompletionSource<TResult>();
            string id = RegisterCallback((resultObject) =>
            {

                tcs.TrySetResult(TypeResolver.Resolve<TResult>(resultObject));
            });

            RegisterError(id, (exception) =>
            {
                tcs.TrySetException(exception);
            });
            var msg = new CallMessage(id, procUri, new object[] { SessionId }.Concat(arguments).ToArray());
            var json = MessageProvider.SerializeMessage(msg);
            Connection.Send(json);

            return tcs.Task;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        void RegisterError(string callID, Action<Exception> exceptionAction)
        {
            lock (_errorMap)
            {
                _errorMap[callID] = exceptionAction;
            }
        }
        string RegisterCallback(Action<object> resultAction)
        {
            lock (_callbackMap)
            {
                string callbackIDString = _callbackID.ToString();
                _callbackMap[callbackIDString] = resultAction;
                _callbackID++;
                return callbackIDString;
            }
        }
        /// <summary>
        /// Method invoked when new WAMP message has been received.
        /// </summary>
        /// <param name="msg"></param>
        protected bool OnMessageReceived(IMessage msg)
        {
            var type = msg.Type;
            switch (type)
            {
                case MessageTypes.Welcome:
                    OnWelcomeReceived((WelcomeMessage)msg);
                    return true;

                case MessageTypes.CallResult:
                    OnCallResult((CallResultMessage)msg);
                    return true;

                case MessageTypes.CallError:
                    OnCallError((CallErrorMessage)msg);
                    return true;

                case MessageTypes.Event:
                    OnEvent((EventMessage)msg);
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Method invoked, when new <see cref="EventMessage"/> has been received.
        /// </summary>
        /// <param name="eventMessage"></param>
        protected virtual void OnEvent(EventMessage eventMessage)
        {
            if (_callbackMap.ContainsKey(eventMessage.TopicUri))
            {
                _callbackMap[eventMessage.TopicUri](eventMessage.EventObject);
            }
        }

        /// <summary>
        /// Method invoked, when new <see cref="CallErrorMessage"/> has been received.
        /// </summary>
        /// <param name="callErrorMessage"></param>
        protected virtual void OnCallError(CallErrorMessage callErrorMessage)
        {
            if (_errorMap.ContainsKey(callErrorMessage.CallId.ToString()))
            {
                _errorMap[callErrorMessage.CallId.ToString()](new Exception(callErrorMessage.ErrorDescription as string));
                _errorMap.Remove(callErrorMessage.CallId.ToString());
            }
        }

        /// <summary>
        /// Method invoked, when new <see cref="CallResultMessage"/> has been received.
        /// </summary>
        /// <param name="callResultMessage"></param>
        protected virtual void OnCallResult(CallResultMessage callResultMessage)
        {
            if (_callbackMap.ContainsKey(callResultMessage.CallId.ToString()))
            {
                _callbackMap[callResultMessage.CallId.ToString()](callResultMessage.Result);
                _callbackMap.Remove(callResultMessage.CallId.ToString());
            }
            else if (callResultMessage.Result == null)
            {
                //do nothing
            }
        }

        /// <summary>
        /// Method invoked when new WAMP welcome message has been received.
        /// </summary>
        /// <param name="msg"></param>
        protected virtual void OnWelcomeReceived(WelcomeMessage msg)
        {
            if (WampConstants.ProtocolVersion != msg.ProtocolVersion)
                throw new ProtocolVersionException(
                    string.Format("Incompatible WAMP protocol versions. Currently accepted version is {0}, but WELCOME version is {1}",
                    WampConstants.ProtocolVersion,
                    msg.ProtocolVersion), msg.ProtocolVersion);

            this.SessionId = msg.SessionId;
            this.IsConnected = true;
        }


    }
}
