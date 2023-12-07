using System;

namespace PolkaDOTS
{
    public interface IArgument
    {
        bool TryParse(string[] arguments);
        string GetArgumentName();
    }
    
    
    /// <summary>
    /// Marks annotated type as an ArgumentClass, which will be processed by <see cref="PolkaDOTS.Configuration.CommandLineParser"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.All, Inherited = false)]
    public class ArgumentClass : Attribute
    { }
    
}