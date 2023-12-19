using System;

namespace PolkaDOTS
{
    
    /// <summary>
    /// Marks a <see cref="ISystem"/> or <see cref="SystemBase"/> class as unique: they will only be added to one
    /// <see cref="World"/>, the first one created that includes them. This is useful, for instance, with Systems
    /// that access MonoBehaviours and having two systems interacting with the same MonoBehaviour would break things
    /// </summary>
    [AttributeUsage(AttributeTargets.All, Inherited = false)]
    public class UniqueSystemAttribute: Attribute
    { }
}