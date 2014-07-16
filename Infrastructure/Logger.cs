using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Logging;

namespace idseefeld.de.UmbracoAzure.Infrastructure
{
    internal class LogAdapter : ILogger
    {
        public void Error<T>(string message, Exception exception)
        {
            LogHelper.Error<T>(message, exception);
        }

        public void Error(Type callingType, string message, Exception exception)
        {
            LogHelper.Error(callingType, message, exception);
        }

        public void Warn(Type callingType, string message, params Func<object>[] formatItems)
        {
            LogHelper.Warn(callingType, message, formatItems);
        }

        public void Warn(Type callingType, string message, bool showHttpTrace, params Func<object>[] formatItems)
        {
            LogHelper.Warn(callingType, message, showHttpTrace, formatItems);
        }

        public void WarnWithException(Type callingType, string message, Exception e, params Func<object>[] formatItems)
        {
            LogHelper.WarnWithException(callingType, message, e, formatItems);
        }

        public void WarnWithException(Type callingType, string message, bool showHttpTrace, Exception e, params Func<object>[] formatItems)
        {
            LogHelper.WarnWithException(callingType, message, showHttpTrace, e, formatItems);
        }

        public void Warn<T>(string message, params Func<object>[] formatItems)
        {
            LogHelper.Warn<T>(message, formatItems);
        }

        public void Warn<T>(string message, bool showHttpTrace, params Func<object>[] formatItems)
        {
            LogHelper.Warn<T>(message, showHttpTrace, formatItems);
        }

        public void WarnWithException<T>(string message, Exception e, params Func<object>[] formatItems)
        {
            LogHelper.WarnWithException<T>(message, e, formatItems);
        }

        public void WarnWithException<T>(string message, bool showHttpTrace, Exception e, params Func<object>[] formatItems)
        {
            LogHelper.WarnWithException<T>(message, showHttpTrace, e, formatItems);
        }

        public void Info<T>(Func<string> generateMessage)
        {
            LogHelper.Info<T>(generateMessage);
        }

        public void Info(Type callingType, Func<string> generateMessage)
        {
            LogHelper.Info(callingType, generateMessage);
        }

        public void Info(Type type, string generateMessageFormat, params Func<object>[] formatItems)
        {
            LogHelper.Info(type, generateMessageFormat, formatItems);
        }

        public void Info<T>(string generateMessageFormat, params Func<object>[] formatItems)
        {
            LogHelper.Info<T>(generateMessageFormat, formatItems);
        }

        public void Debug<T>(Func<string> generateMessage)
        {
            LogHelper.Debug<T>(generateMessage);
        }

        public void Debug(Type callingType, Func<string> generateMessage)
        {
            LogHelper.Debug(callingType, generateMessage);
        }

        public void Debug(Type type, string generateMessageFormat, params Func<object>[] formatItems)
        {
            LogHelper.Debug(type, generateMessageFormat, formatItems);
        }

        public void Debug<T>(string generateMessageFormat, params Func<object>[] formatItems)
        {
            LogHelper.Debug<T>(generateMessageFormat, formatItems);
        }

        public void Debug<T>(string generateMessageFormat, bool showHttpTrace, params Func<object>[] formatItems)
        {
            LogHelper.Debug<T>(generateMessageFormat, showHttpTrace, formatItems);
        }
    }
}
