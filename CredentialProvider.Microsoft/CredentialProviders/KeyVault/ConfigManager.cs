// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.Configuration;

namespace Microsoft.Azure.KeyVault.Helper
{
    public class ConfigManager
    {
        private readonly Configuration config;

        /// <summary>
        /// Opens default .exe config to read values from it
        /// </summary>
        public ConfigManager()
        {
            config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        }

        /// <summary>
        /// Opens config specified at <paramref name="configFilePath"/> to read values from it
        /// </summary>
        public ConfigManager(string configFilePath)
        {
            var map = new ExeConfigurationFileMap()
            {
                ExeConfigFilename = configFilePath
            };
            config = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);
        }

        public string GetSetting(string setting)
        {
            return config.AppSettings?.Settings[setting]?.Value;
        }

        public void SetSetting(string setting, string value)
        {
            var settings = config.AppSettings.Settings;
            if (settings[setting] == null)
            {
                settings.Add(setting, value);
            }
            else
            {
                settings[setting].Value = value;
            }
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection(config.AppSettings.SectionInformation.Name);

        }

        public ConfigurationSection GetSection(string section)
        {
            return config.GetSection(section);
        }
    }
}
