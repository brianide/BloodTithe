using Newtonsoft.Json;
using System.ComponentModel;
using System.IO;
using Terraria.ID;
using TShockAPI;

namespace BloodTithe
{
	class BloodTitheConfig
	{
		private static readonly string FilePath = Path.Combine(TShock.SavePath, "BloodTithe.json");
		private static readonly BloodTitheConfig Default = JsonConvert.DeserializeObject<BloodTitheConfig>("{}");

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue(ItemID.LifeCrystal)]
		public int Item { get; private set; }

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue(3)]
		public int ItemsRequired { get; private set; }

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue(false)]
		public bool PreventCorruption { get; private set; }

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue(true)]
		public bool SpawnWarpFairy { get; private set; }

		public static BloodTitheConfig Load()
		{
			TShock.Log.Info("Loading configuration from {0}", FilePath);

			BloodTitheConfig config;

			if (!File.Exists(FilePath))
			{
				config = Default;
			}
			else
			{
				try
				{
					config = JsonConvert.DeserializeObject<BloodTitheConfig>(File.ReadAllText(FilePath));
				}
				catch (JsonReaderException e)
				{
					TShock.Log.Error("Error loading {0}: {1}", FilePath, e.Message);
					config = Default;
				}
			}

			File.WriteAllText(FilePath, JsonConvert.SerializeObject(config, Formatting.Indented));
			return config;
		}

		public override string ToString() => JsonConvert.SerializeObject(this);
	}
}