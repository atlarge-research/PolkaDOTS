using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Unity.Logging.Internal.Debug;
using UnityEngine;

namespace PolkaDOTS.Configuration
{

    /// <summary>
    /// Class to parse configuration parameters from arguments. Called by <see cref="CmdArgsReader"/>
    /// </summary>
    public static class CommandLineParser
    {
        internal delegate bool TryParseDelegate<T>(string[] arguments, string argumentName, out T result);
        
        public abstract class BaseArgument<T> : IArgument
        {
            /// <summary>
            ///
            /// </summary>
            public string ArgumentName { get; }

            /// <summary>
            /// 
            /// </summary>
            public bool Defined => m_defined;

            /// <summary>
            /// 
            /// </summary>
            public readonly bool Required;

            /// <summary>
            /// 
            /// </summary>
            public T Value => m_value;

            /// <summary>
            /// 
            /// </summary>
            public void SetValue(T val)
            {
                m_value = val;
            }
            
            /// <summary>
            /// 
            /// </summary>
            protected T Default;

            protected bool m_defined;
            protected T m_value;
            readonly TryParseDelegate<T> m_parser;

            protected abstract bool DefaultParser(string[] arguments, string argumentName, out T parsedResult);

            public bool TryParse(string[] arguments)
            {
                m_defined =
                    m_parser != null &&
                    m_parser(arguments, ArgumentName, out m_value);
                /*if (!m_defined)
                {
                    m_value = Default;
                }*/
                return m_defined;
            }

            public string GetArgumentName()
            {
                return ArgumentName;
            }

            internal BaseArgument(string argumentName, T defaultValue, bool required = false)
            {
                Required = required;
                ArgumentName = argumentName;
                Default = defaultValue;
                m_parser = DefaultParser;
            }
            
        }

        public class StringArgument : BaseArgument<string>
        {
            protected override bool DefaultParser(string[] arguments, string argumentName, out string parsedResult) => TryParseStringArgument(arguments, argumentName, out parsedResult, Default, Required);

            public StringArgument(string argumentName, string defaultVal, bool required = false) : base(argumentName, defaultVal, required) { }

            //public static implicit operator string(StringArgument argument) => !argument.Defined ? argument.Default : argument.Value;
            public static implicit operator string(StringArgument argument) => argument.Value;
        }

        public class EnumArgument<T> : BaseArgument<T> where T : struct
        {
            protected override bool DefaultParser(string[] arguments, string argumentName, out T parsedResult) => TryParseEnumArgument(arguments, argumentName, out parsedResult, Default, Required);

            public EnumArgument(string argumentName, T defaultVal, bool required = false) : base(argumentName, defaultVal, required) { }

            public static implicit operator T(EnumArgument<T> argument) => argument.Value;
        }

        public class StringArrayArgument : BaseArgument<string[]>
        {
            protected override bool DefaultParser(string[] arguments, string argumentName, out string[] parsedResult) => TryParseStringArrayArgument(arguments, argumentName, out parsedResult, Default, Required);

            public StringArrayArgument(string argumentName, string[] defaultVal, bool required = false) : base(argumentName, defaultVal, required) { }

            public static implicit operator string[](StringArrayArgument argument) => argument.Value;
        }


        public class IntArgument : BaseArgument<int>
        {
            protected override bool DefaultParser(string[] arguments, string argumentName, out int parsedResult) => TryParseIntArgument(arguments, argumentName, out parsedResult, Default, Required);

            public IntArgument(string argumentName, int defaultVal, bool required = false) : base(argumentName, defaultVal, required) { }

            public static implicit operator int(IntArgument argument) => argument.Value;
        }
        
        public class FlagArgument : BaseArgument<bool>
        {
            protected override bool DefaultParser(string[] arguments, string argumentName, out bool parsedResult) => TryParseFlagArgument(arguments, argumentName, out parsedResult, Required);

            public FlagArgument(string argumentName, bool defaultVal, bool required = false) : base(argumentName, defaultVal, required) { }

            public static implicit operator bool(FlagArgument argument) => argument.Value;
        }

        public class JsonFileArgument<T> : BaseArgument<T?> where T : struct
        {
            protected override bool DefaultParser(string[] arguments, string argumentName, out T? parsedResult) => TryParseJsonFileArgument(arguments, argumentName, out parsedResult, Required);

            public JsonFileArgument(string argumentName, T? defaultVal, bool required = false) : base(argumentName, defaultVal, required) { }

            public static implicit operator T?(JsonFileArgument<T> argument) => argument.Value;
        }
        
        /*public class JsonFileArgumentClass<T> : BaseArgument<T> where T : class
        {
            protected override bool DefaultParser(string[] arguments, string argumentName, out T parsedResult) => TryParseJsonFileArgumentClass(arguments, argumentName, out parsedResult, Required);

            public JsonFileArgumentClass(string argumentName, T defaultVal, bool required = false) : base(argumentName, defaultVal, required) { }

            public static implicit operator T(JsonFileArgumentClass<T> argument) => argument.Value;
        }*/
        
        public class FilePathArgument : BaseArgument<string>
        {
            protected override bool DefaultParser(string[] arguments, string argumentName, out string parsedResult) => TryParseFilePathArgument(arguments, argumentName, out parsedResult, Default);
            
            public FilePathArgument(string argumentName, string defaultVal, bool required = false) : base(argumentName, defaultVal, required) { }

            public static implicit operator string(FilePathArgument argument) => argument.Value;
        }

        static bool TryParseStringArgument(string[] arguments, string argumentName, out string argumentValue, string def, bool required = false)
        {
            var startIndex = System.Array.FindIndex(arguments, x => x == argumentName);
            if (startIndex < 0)
            {
                argumentValue = def;
                return !required;
            }

            if (startIndex + 1 >= arguments.Length)
            {
                argumentValue = def;
                return false;
            }

            argumentValue = arguments[startIndex + 1];
            return !string.IsNullOrEmpty(argumentValue);
        }

        static bool TryParseEnumArgument<T>(string[] arguments, string argumentName, out T argumentValue, T def,
            bool required = false) where T : struct
        {
            bool result = TryParseStringArgument(arguments, argumentName, out string value, "", required);
            if (result && !string.IsNullOrEmpty(value))
            {
                if (Enum.TryParse(value, true, out T enumValue))
                {
                    argumentValue = enumValue;
                    return true;
                }
                result = false;
            }
            argumentValue = def;
            return result;
        }


        static bool TryParseIntArgument(string[] arguments, string argumentName, out int argumentValue, int def, bool required = false)
        {
            var startIndex = System.Array.FindIndex(arguments, x => x == argumentName);
            if (startIndex < 0)
            {
                argumentValue = def;
                return !required;
            }

            if (startIndex + 1 >= arguments.Length)
            {
                argumentValue = def;
                return false;
            }

            if (!int.TryParse(arguments[startIndex + 1], out var result))
            {
                argumentValue = def;
                return false;
            }
            argumentValue = result;
            return true;
        }
        
        static bool TryParseFlagArgument(string[] arguments, string argumentName, out bool argumentValue, bool required = false)
        {
            var startIndex = System.Array.FindIndex(arguments, x => x == argumentName);
            if (startIndex < 0)
                argumentValue = false;
            else
                argumentValue = true;
            return true;
        }

        static bool TryParseStringArrayArgument(string[] arguments, string argumentName, out string[] argumentValue, string[] def, bool required = false)
        {
            List<string> list = new List<string>();

            for (int i = 0; i < arguments.Length;)
            {
                var startIndex = Array.FindIndex(arguments, i, x => x == argumentName);
                if (startIndex < 0)
                    break;
                if (startIndex + 1 >= arguments.Length)
                    break;
                list.Add(arguments[startIndex + 1]);
                i = startIndex + 2;
            }
            if (list.Count == 0 && required)
            {
                argumentValue = def;
                return false;
            }
            argumentValue = list.ToArray();
            return true;
        }

        static bool TryParseJsonFileArgument<T>(string[] arguments, string argumentName, out T? argumentValue,
            bool required = false) where T : struct
        {
            bool result = TryParseFilePathArgument(arguments, argumentName, out string value, "");
            if (result && !string.IsNullOrEmpty(value))
            {
                string text = "";
                if (File.Exists(value))
                {
                    text = File.ReadAllText(value);
                }
                else //If file operation fails, attempt to load it from resources
                {
                    Debug.LogWarning($"[CONFIG] json argument file {value} not found, attempting to load from Assets/Resources/{value}");
                    TextAsset jsonTxt = Resources.Load<TextAsset>(value);
                    if (jsonTxt != null)
                    {
                        text = jsonTxt.text;
                        Debug.Log($"[CONFIG]json arg found in resources!");
                    }
                    else
                    {
                        Debug.LogError($"[CONFIG] jsonargument not {value} found");
                    }
                }

                try
                {
                    //argumentValue = JsonUtility.FromJson<T>(text);
                    argumentValue = JsonConvert.DeserializeObject<T>(text);
                    return true;
                }
                catch (Exception)
                {
                    result = false;
                }
            }
            else if (required)
                result = false;
            argumentValue = null;
            return result;
        }

        static bool TryParseFilePathArgument(string[] arguments, string argumentName, out string argumentValue, string def)
        {
            bool ret = TryParseStringArgument(arguments, argumentName, out string value, "");
            if (!ret)
            {
                argumentValue = def;
                return false;
            }
            if (string.IsNullOrEmpty(value))
            {
                argumentValue = def;
                return true;
            }

            /*if (!File.Exists(value))
            {
                argumentValue = null;
                return false;
            }*/
            argumentValue = value;
            return true;
        }
        
        /// <summary>
        /// Finds all classes with a particular attribute type
        /// </summary>
        /// <param name="inherit">Whether to includes class that indirectly inherent the attribute</param>
        /// <returns>List of Types with the given attribute.</returns>
        /// <remarks>See https://stackoverflow.com/questions/720157/finding-all-classes-with-a-particular-attribute</remarks>
        public static IEnumerable<Type> GetTypesWith<TAttribute>(bool inherit=false) where TAttribute : Attribute
        {
            var output = new List<Type>();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                var assembly_types = assembly.GetTypes();

                foreach (var type in assembly_types)
                {
                    if (type.IsDefined(typeof(TAttribute), inherit))
                        output.Add(type);
                }
            }

            return output;
        }

        /// <summary>
        /// Calls TryParse on all Argument objects in all classes marked with ArgumentClass 
        /// </summary>
        /// <param name="commandLineArgs">List of string arguments from command line</param>
        /// <returns>True if all arguments parsed successfully</returns>
        /// <remarks>See https://stackoverflow.com/questions/5976216/how-to-call-an-explicitly-implemented-interface-method-on-the-base-class</remarks>
        internal static bool TryParse(string[] commandLineArgs)
        { 
            var argumentClasses = GetTypesWith<ArgumentClass>();
            foreach (var argumentClassType in argumentClasses)
            {
                FieldInfo[] fields = argumentClassType.GetFields(BindingFlags.Public | BindingFlags.Static);
                foreach (FieldInfo field in fields)
                {
                    var argumentField = field.GetValue(null);
                    var argumentFieldBaseType = field.FieldType.BaseType;
                    if (argumentFieldBaseType != null)
                    {
                        var methods = argumentFieldBaseType.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                                    BindingFlags.DeclaredOnly);
                        foreach (var method in methods)
                        {
                            if (method.Name.Contains("TryParse"))
                            {
                                bool ret = (bool)method.Invoke(argumentField, new object[]{commandLineArgs});
                                if (!ret)
                                {
                                    Debug.Log($"Failed to read argument {field.Name}");
                                    return false;
                                }
                                break;
                            }
                        }
                    }
                }
            }
            return true;
        }
    }
}