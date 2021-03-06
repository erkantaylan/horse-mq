using System.Threading.Tasks;
using Horse.Mq.Clients;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Routing
{
    /// <summary>
    /// Router implementation.
    /// A router, routes messages to its' bindings.
    /// Horse MQ Server uses Router class for server specific routers.
    /// But if you need custom Router, you can implement IRouter and create your own router.
    /// </summary>
    public interface IRouter
    {
        /// <summary>
        /// Route name.
        /// Must be unique.
        /// Can't include " ", "*" or ";"
        /// </summary>
        string Name { get; }

        /// <summary>
        /// If true, messages are routed to bindings.
        /// If false, messages are not routed.
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// Gets all bindings of router
        /// </summary>
        Binding[] GetBindings();

        /// <summary>
        /// Adds new binding to router
        /// </summary>
        bool AddBinding(Binding binding);

        /// <summary>
        /// Removes a binding from the route
        /// </summary>
        void RemoveBinding(string bindingName);

        /// <summary>
        /// Removes a binding from the route
        /// </summary>
        void RemoveBinding(Binding binding);

        /// <summary>
        /// Pushes a message to router
        /// </summary>
        Task<RouterPublishResult> Publish(MqClient sender, HorseMessage message);
    }
}