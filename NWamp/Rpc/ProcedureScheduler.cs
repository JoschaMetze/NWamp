using NWamp.Messages;
using NWamp.Transport;
using System;
#if !WINDOWS_PHONE
using System.Collections.Concurrent;
#endif
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Reflection;
using System.Linq;

namespace NWamp.Rpc
{
    /// <summary>
    /// Implementation of <see cref="IProcedureScheduler"/>, responsible for managing remote procedure calls.
    /// </summary>
    public class ProcedureScheduler : IProcedureScheduler
    {
        /// <summary>
        /// Response queue used for passing results received from invoked RPC.
        /// </summary>
        protected IResponseQueue Response;

        /// <summary>
        /// Type resolver used for converting deserialized JSON object into specific type instances.
        /// </summary>
        protected ITypeResolver TypeResolver;

        /// <summary>
        /// Collection of <see cref="Task"/> objects, handling RPC which are currently executing.
        /// </summary>
#if !WINDOWS_PHONE
        protected ConcurrentDictionary<string, Task> Tasks;
#else
        protected SynchronizedCache<string,Task> Tasks;
#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcedureScheduler"/> class.
        /// </summary>
        /// <param name="responseQueue">Response queue used for sending RPC results back to client</param>
        /// <param name="typeResolver">Resolver used for converting deserialized JSON object into specific type instances</param>
        public ProcedureScheduler(IResponseQueue responseQueue, ITypeResolver typeResolver)
        {
            Response = responseQueue;
            TypeResolver = typeResolver;
#if !WINDOWS_PHONE
            Tasks = new ConcurrentDictionary<string, Task>();
#else
            Tasks = new SynchronizedCache<string, Task>();
#endif
        }

        /// <summary>
        /// Schedules new RPC request.
        /// </summary>
        /// <param name="context"></param>
        public void Schedule(ProcedureContext context)
        {
            ResolveProcedureArgumentsTypes(context);

            var callId = context.CallId;
            var handler = CreateProcedureHandler(context);
            var task = new Task(handler);

            if (Tasks.TryAdd(callId, task))
            {
                task.Start();
                task.ContinueWith(t =>
                {
                    Task outTask;
                    Tasks.TryRemove(callId, out outTask);
                });
            }
        }
        //Method to handle async results
        private static void ContinueAsync<T>(Task<T> task, TaskCompletionSource<object> tcs)
        {
            task.ContinueWith(t =>
            {
                if (t.IsCanceled)
                {
                    tcs.TrySetCanceled();
                }
                else if (t.IsFaulted)
                {
                    tcs.TrySetException(t.Exception);
                }
                else
                {
                    tcs.TrySetResult(t.Result);
                }
            });
        }

        /// <summary>
        /// Returns a delegate used for handling RPC invoke flow, 
        /// including handling exception and returning WAMP result messages.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected virtual Action CreateProcedureHandler(ProcedureContext context)
        {
            Action action = () =>
                {
                    IMessage message;
                    try
                    {
                        var func = context.ProcedureDefinition.ProcedureCall;
                        var args = context.Arguments;
                        var result = func(args);
                        //make sure to handle async results correctly

                        if (result != null && result.GetType() == typeof(Task))
                        {
                            (result as Task).ContinueWith((task) =>
                            {
                                message = CreateResultMessage(context, result);
                                Response.Send(context.RequesterSession.SessionId, message);
                            });
                        }
                        else if (result != null && result.GetType().IsGenericType && result.GetType().GetGenericTypeDefinition().BaseType == typeof(Task))
                        {

                            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
                            Type resultType = result.GetType().GetGenericArguments().Single();

                            Type genericTaskType = typeof(Task<>).MakeGenericType(resultType);


                            var parameter = Expression.Parameter(typeof(object), "object");


                            MethodInfo continueWithMethod = typeof(ProcedureScheduler).GetMethod("ContinueAsync", BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(resultType);

                            Expression body = Expression.Call(continueWithMethod,
                                                              Expression.Convert(parameter, genericTaskType),
                                                              Expression.Constant(tcs));

                            var continueWithInvoker = Expression.Lambda<Action<object>>(body, parameter).Compile();
                            continueWithInvoker.Invoke(result);
                            tcs.Task.ContinueWith((task) =>
                            {
                                if (task.IsFaulted)
                                {
                                    Exception exc = task.Exception.InnerException;
                                    if (exc.InnerException != null)
                                        exc = exc.InnerException;

                                    message = CreateErrorMessage(context, exc);
                                    Response.Send(context.RequesterSession.SessionId, message);
                                }
                                else
                                {
                                    message = CreateResultMessage(context, task.Result);
                                    Response.Send(context.RequesterSession.SessionId, message);
                                }
                            });

                        }
                        else
                        {
                            message = CreateResultMessage(context, result);
                            Response.Send(context.RequesterSession.SessionId, message);
                        }
                    }
                    catch (Exception e)
                    {
                        message = CreateErrorMessage(context, e);
                        Response.Send(context.RequesterSession.SessionId, message);
                    }
                    
                };

            return action;
        }

        /// <summary>
        /// Creates a new instance of <see cref="CallResultMessage"/> class.
        /// </summary>
        /// <param name="context">Context of RPC request</param>
        /// <param name="result">Result of successfull RPC exection</param>
        /// <returns></returns>
        protected virtual CallResultMessage CreateResultMessage(ProcedureContext context, object result)
        {
            return new CallResultMessage(context.CallId, result);
        }

        /// <summary>
        /// Creates a new instance of <see cref="CallErrorMessage"/> class.
        /// </summary>
        /// <param name="context">Context of RPC request</param>
        /// <param name="exception">Exception, which ocurred during RPC exection</param>
        /// <returns></returns>
        protected virtual CallErrorMessage CreateErrorMessage(ProcedureContext context, Exception exception)
        {
            return new CallErrorMessage(context.CallId, string.Empty, exception.Message);
        }

        /// <summary>
        /// Converts provided procedure call arguments into destinated types.
        /// </summary>
        /// <param name="context"></param>
        protected virtual void ResolveProcedureArgumentsTypes(ProcedureContext context)
        {
            for (int i = 0; i < context.Arguments.Length; i++)
            {
                var argType = context.ProcedureDefinition.ArgumentTypes[i];
                var destinationObject = TypeResolver.Resolve(context.Arguments[i], argType);
                context.Arguments[i] = destinationObject;
            }
        }
    }
}
