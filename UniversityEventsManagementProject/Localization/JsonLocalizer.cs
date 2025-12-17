using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Localization;

namespace UniversityEventsManagementProject.Localization
{
    public class JsonLocalizer : IStringLocalizer
    {
        private readonly Dictionary<string, string> _localizations;
        private readonly string _culture;

        public JsonLocalizer(string culture)
        {
            _culture = culture;
            _localizations = LoadLocalizations(culture);
        }

        private Dictionary<string, string> LoadLocalizations(string culture)
        {
            var localizations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            try
            {
                // Try multiple paths
                var paths = new[]
                {
                    Path.Combine("Localization", $"{culture}.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Localization", $"{culture}.json"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Localization", $"{culture}.json")
                };

                string? jsonPath = null;
                foreach (var path in paths)
                {
                    if (File.Exists(path))
                    {
                        jsonPath = path;
                        break;
                    }
                }
                
                if (jsonPath != null)
                {
                    var jsonContent = File.ReadAllText(jsonPath);
                    var jsonDoc = JsonDocument.Parse(jsonContent);
                    
                    foreach (var property in jsonDoc.RootElement.EnumerateObject())
                    {
                        localizations[property.Name] = property.Value.GetString() ?? property.Name;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error if needed
                Console.WriteLine($"Error loading localizations for {culture}: {ex.Message}");
            }

            return localizations;
        }

        public LocalizedString this[string name]
        {
            get
            {
                if (_localizations.TryGetValue(name, out var value))
                {
                    return new LocalizedString(name, value, false);
                }
                return new LocalizedString(name, name, true);
            }
        }

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                var localizedString = this[name];
                if (localizedString.ResourceNotFound)
                {
                    return localizedString;
                }
                
                try
                {
                    var formattedValue = string.Format(localizedString.Value, arguments);
                    return new LocalizedString(name, formattedValue, false);
                }
                catch
                {
                    return localizedString;
                }
            }
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            return _localizations.Select(kvp => new LocalizedString(kvp.Key, kvp.Value, false));
        }
    }

    public class JsonStringLocalizerFactory : IStringLocalizerFactory
    {
        private readonly Dictionary<string, JsonLocalizer> _localizers = new();

        public IStringLocalizer Create(Type resourceSource)
        {
            var culture = CultureInfo.CurrentUICulture.Name;
            if (culture.Length > 2)
            {
                culture = culture.Substring(0, 2);
            }
            
            if (!_localizers.ContainsKey(culture))
            {
                _localizers[culture] = new JsonLocalizer(culture);
            }
            
            return _localizers[culture];
        }

        public IStringLocalizer Create(string baseName, string location)
        {
            var culture = CultureInfo.CurrentUICulture.Name;
            if (culture.Length > 2)
            {
                culture = culture.Substring(0, 2);
            }
            
            if (!_localizers.ContainsKey(culture))
            {
                _localizers[culture] = new JsonLocalizer(culture);
            }
            
            return _localizers[culture];
        }
    }

    public class JsonStringLocalizer<T> : IStringLocalizer<T>
    {
        private readonly IStringLocalizer _localizer;

        public JsonStringLocalizer(IStringLocalizerFactory factory)
        {
            _localizer = factory.Create(typeof(T));
        }

        public LocalizedString this[string name] => _localizer[name];

        public LocalizedString this[string name, params object[] arguments] => _localizer[name, arguments];

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            return _localizer.GetAllStrings(includeParentCultures);
        }
    }

    // Simple wrapper for non-generic IStringLocalizer
    public class JsonStringLocalizer : IStringLocalizer
    {
        private readonly IStringLocalizer _localizer;

        public JsonStringLocalizer(IStringLocalizerFactory factory)
        {
            _localizer = factory.Create("SharedResources", "Localization");
        }

        public LocalizedString this[string name] => _localizer[name];

        public LocalizedString this[string name, params object[] arguments] => _localizer[name, arguments];

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            return _localizer.GetAllStrings(includeParentCultures);
        }
    }
}

