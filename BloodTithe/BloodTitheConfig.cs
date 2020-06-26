using CommonGround.Configuration;
using System.ComponentModel;
using System.IO;
using Terraria.ID;
using TShockAPI;

namespace BloodTithe
{
	struct BloodTitheConfig : IPluginConfiguration
	{
		public string FilePath => Path.Combine(TShock.SavePath, "BloodTithe.json");

		[DefaultValue(ItemID.LifeCrystal)]
		public int Item { get; private set; }

		[DefaultValue(3)]
		public int ItemsRequired { get; private set; }

		[DefaultValue(false)]
		public bool PreventCorruption { get; private set; }

		[DefaultValue(true)]
		public bool SpawnWarpFairy { get; private set; }

	}
}