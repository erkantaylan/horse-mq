using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Twino.Client.TMQ.Models;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Bus
{
    /// <summary>
    /// Implementation for queue messages and requests
    /// </summary>
    public interface ITwinoQueueBus : ITwinoConnection
    {
        /// <summary>
        /// Pushes a message into a queue
        /// </summary>
        /// <param name="channel">Target channel name</param>
        /// <param name="queueId">Target Queue Id</param>
        /// <param name="content">Message content</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<TwinoResult> Push(string channel, ushort queueId, MemoryStream content, bool waitAcknowledge = false,
                               IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Pushes a message into a queue
        /// </summary>
        /// <param name="channel">Target channel name</param>
        /// <param name="queueId">Target Queue Id</param>
        /// <param name="content">Message content</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<TwinoResult> Push(string channel, ushort queueId, string content, bool waitAcknowledge = false,
                               IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Pushes a JSON message into a queue
        /// </summary>
        /// <param name="jsonObject">The object that will be serialized to JSON string</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<TwinoResult> PushJson(object jsonObject, bool waitAcknowledge = false,
                                   IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Pushes a JSON message into a specified queue
        /// </summary>
        /// <param name="channel">Target channel name</param>
        /// <param name="queueId">Target Queue Id</param>
        /// <param name="jsonObject">The object that will be serialized to JSON string</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<TwinoResult> PushJson(string channel, ushort queueId, object jsonObject, bool waitAcknowledge = false,
                                   IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Request a pull request
        /// </summary>
        /// <param name="request">Pull request object</param>
        /// <param name="actionForEachMessage">Action for each pulled messages</param>
        /// <returns></returns>
        Task<PullContainer> Pull(PullRequest request, Func<int, TmqMessage, Task> actionForEachMessage = null);
    }
}