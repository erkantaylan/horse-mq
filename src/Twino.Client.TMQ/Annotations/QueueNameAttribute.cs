using System;

namespace Twino.Client.TMQ.Annotations
{
    /// <summary>
    /// Queue Name attribute for queue messages
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class QueueNameAttribute : Attribute
    {
        /// <summary>
        /// The queue name for the type
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Creates new queue name attribute
        /// </summary>
        public QueueNameAttribute(string name)
        {
            Name = name;
        }
    }
}