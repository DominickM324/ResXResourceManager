﻿namespace tomenglertde.ResXManager.Model
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    using AutoProperties;

    using JetBrains.Annotations;

    using tomenglertde.ResXManager.Infrastructure;

    using TomsToolbox.Core;
    using TomsToolbox.Desktop;

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class DefaultValueAttribute : Attribute
    {
        [NotNull]
        public object Value { get; }

        public DefaultValueAttribute([NotNull] object value)
        {
            Value = value;
        }
    }

    /// <summary>
    /// Handle global persistence.
    /// </summary>
    public abstract class ConfigurationBase : ObservableObject
    {
        private const string FileName = "Configuration.xml";

        [NotNull]
        // ReSharper disable once AssignNullToNotNullAttribute
        private static readonly string _directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "tom-englert.de", "ResXManager");
        [NotNull]
        private readonly string _filePath;
        [NotNull]
        private readonly XmlConfiguration _configuration;

        protected ConfigurationBase([NotNull] ITracer tracer)
        {
            Contract.Requires(tracer != null);

            Contract.Assume(!string.IsNullOrEmpty(_directory));

            Tracer = tracer;
            _filePath = Path.Combine(_directory, FileName);

            try
            {
                Directory.CreateDirectory(_directory);

                using (var reader = new StreamReader(File.OpenRead(_filePath)))
                {
                    _configuration = new XmlConfiguration(tracer, reader);
                    return;
                }
            }
            catch
            {
            }

            _configuration = new XmlConfiguration(tracer);
        }

        public abstract bool IsScopeSupported
        {
            get;
        }

        public abstract ConfigurationScope Scope
        {
            get;
        }

        [NotNull, InterceptIgnore]
        protected ITracer Tracer { get; }

        [CanBeNull, GetInterceptor, UsedImplicitly]
        protected T GetProperty<T>([NotNull] string key, [CanBeNull] PropertyInfo propertyInfo)
        {
            Contract.Requires(key != null);

            return GetValue(GetDefaultValue<T>(propertyInfo), key);
        }

        [CanBeNull]
        protected T GetValue<T>([CanBeNull] T defaultValue, [NotNull] string key)
        {
            Contract.Requires(key != null);

            try
            {
                return InternalGetValue(defaultValue, key);
            }
            catch (InvalidCastException)
            {
            }

            return defaultValue;
        }

        [CanBeNull]
        protected virtual T InternalGetValue<T>([CanBeNull] T defaultValue, [NotNull] string key)
        {
            Contract.Requires(key != null);

            return ConvertFromString<T>(_configuration.GetValue(key, ConvertToString(defaultValue)));
        }

        [SetInterceptor, UsedImplicitly]
        protected void SetValue<T>([CanBeNull] T value, [NotNull] string key)
        {
            Contract.Requires(key != null);

            InternalSetValue(value, key);
        }

        protected virtual void InternalSetValue<T>([CanBeNull] T value, [NotNull] string key)
        {
            Contract.Requires(key != null);

            try
            {
                _configuration.SetValue(key, ConvertToString(value));

                using (var writer = new StreamWriter(File.Create(_filePath)))
                {
                    _configuration.Save(writer);
                }
            }
            catch (Exception ex)
            {
                Tracer.TraceError("Fatal error writing configuration file: " + _filePath + " - " + ex.Message);
            }
        }

        [CanBeNull]
        protected static T ConvertFromString<T>([CanBeNull] string value)
        {
            try
            {
                if (!string.IsNullOrEmpty(value))
                {
                    var typeConverter = GetTypeConverter(typeof(T));
                    var obj = typeConverter.ConvertFromInvariantString(value);
                    return obj == null ? default(T) : (T)obj;
                }
            }
            catch (NotSupportedException)
            {
            }

            return default(T);
        }

        [CanBeNull]
        protected static string ConvertToString<T>([CanBeNull] T value)
        {
            if (ReferenceEquals(value, null))
                return null;

            var typeConverter = GetTypeConverter(typeof(T));
            return typeConverter.ConvertToInvariantString(value);
        }

        [NotNull]
        private static TypeConverter GetTypeConverter([NotNull] Type type)
        {
            Contract.Requires(type != null);
            Contract.Ensures(Contract.Result<TypeConverter>() != null);

            return type.GetCustomTypeConverter() ?? TypeDescriptor.GetConverter(type);
        }

        [CanBeNull]
        private static T GetDefaultValue<T>([CanBeNull] MemberInfo propertyInfo)
        {
            var defaultValueAttribute = propertyInfo?.GetCustomAttributes<DefaultValueAttribute>().Select(attr => attr?.Value).FirstOrDefault();

            switch (defaultValueAttribute)
            {
                case T defaultValue:
                    return defaultValue;
                case string stringValue:
                    return ConvertFromString<T>(stringValue);
            }

            return default(T);
        }

        [ContractInvariantMethod]
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Required for code contracts.")]
        [Conditional("CONTRACTS_FULL")]
        private void ObjectInvariant()
        {
            Contract.Invariant(Tracer != null);
            Contract.Invariant(_configuration != null);
            Contract.Invariant(!string.IsNullOrEmpty(_filePath));
            Contract.Invariant(!string.IsNullOrEmpty(_directory));
        }
    }
}
