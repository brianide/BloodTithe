using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;
using System.Reflection;
using TShockAPI;

namespace BloodTithe
{
	interface IPluginConfiguration {
		[JsonIgnore]
		string FilePath { get; }
	}

	class DefaultValueContractResolver : DefaultContractResolver
	{
		protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
		{
			var prop = base.CreateProperty(member, memberSerialization);
			bool tagged = member.GetCustomAttribute<System.ComponentModel.DefaultValueAttribute>() != null && (member as PropertyInfo)?.SetMethod != null;
			prop.Writable = tagged;
			prop.ShouldSerialize = _ => tagged;
			prop.ShouldDeserialize = _ => tagged;
			return prop;
		}
	}

	static class PluginConfiguration
	{
		private readonly static JsonSerializerSettings settings = new JsonSerializerSettings() {
			DefaultValueHandling = DefaultValueHandling.Populate,
			ContractResolver = new DefaultValueContractResolver()
		};

		public static T LoadDefault<T>() => JsonConvert.DeserializeObject<T>("{}", settings);

		public static T Load<T>() where T : IPluginConfiguration
		{
			string filePath = default(T).FilePath;
			TShock.Log.Info("Loading configuration from {0}", filePath);

			T config;
			if (!File.Exists(filePath))
			{
				config = LoadDefault<T>();
			}
			else
			{
				try
				{
					config = JsonConvert.DeserializeObject<T>(File.ReadAllText(filePath), settings);
				}
				catch (JsonReaderException e)
				{
					TShock.Log.Error("Error loading {0}: {1}", filePath, e.Message);
					config = LoadDefault<T>();
				}
			}

			File.WriteAllText(filePath, JsonConvert.SerializeObject(config, Formatting.Indented));
			return config;
		}

		public static string Stringify<T>(T config) where T : IPluginConfiguration => JsonConvert.SerializeObject(config);
	}

}
