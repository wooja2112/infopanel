using System;
using System.Globalization;

namespace InfoPanel.Tests.TestSupport
{
    /// <summary>
    /// Temporarily switches the current thread's culture. CurrentCulture flows with the
    /// execution context, so this is safe under xUnit's parallel test execution.
    /// </summary>
    internal sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _culture;
        private readonly CultureInfo _uiCulture;

        public CultureScope(string name)
        {
            _culture = CultureInfo.CurrentCulture;
            _uiCulture = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _uiCulture;
        }
    }
}
