using Microsoft.Xna.Framework;
using OTAPI.Tile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;
using TShockAPI;

namespace BloodTithe
{
	public static class Extensions
	{
		public static void Let<T>(this T thing, Action<T> action) => action.Invoke(thing);
		public static R Let<T, R>(this T thing, Func<T, R> func) => func.Invoke(thing);

		public static TV GetValue<TK, TV>(this IDictionary<TK, TV> dict, TK key, TV defaultValue = default(TV)) => dict.TryGetValue(key, out TV value) ? value : defaultValue;
		public static V GetValue<K, V>(this ConditionalWeakTable<K, V> thing, K key, V defaultValue = default)
			where K : class
			where V : class
		{
			return thing.TryGetValue(key, out V value) ? value : defaultValue;
		}
		public static void Deconstruct<TK, TV>(this KeyValuePair<TK, TV> thing, out TK key, out TV value) { key = thing.Key; value = thing.Value; }

		public static bool IsInfectious(this ITile thing) => TileID.Sets.Corrupt[thing.type] || TileID.Sets.Crimson[thing.type] || TileID.Sets.Hallow[thing.type];
		public static bool IsStone(this ITile thing) => TileID.Sets.Conversion.Stone[thing.type];

		public static ITile TileAt(this ITileCollection thing, Point tileCoord) => thing[tileCoord.X, tileCoord.Y];

		public static bool NextBoolean(this UnifiedRandom thing) => thing.Next(2) == 0;
		public static int NextDirection(this UnifiedRandom thing) => thing.NextBoolean() ? 1 : -1;

		public static void Deconstruct(this Point thing, out int x, out int y) { x = thing.X; y = thing.Y; }
		public static void Deconstruct(this Vector2 thing, out float x, out float y) { x = thing.X; y = thing.Y; }
	}

	public static class Util
	{
		public static IEnumerable<(Item item, int index)> GetItemsOfType(int itemID) =>
			Main.item
				.Select((item, index) => (item, index))
				.Where(t => t.item.type == itemID && t.item.stack > 0);

		public static (Rectangle bounds, Point tileOrigin) GetAltarBoundsFromTile(int x, int y, float fluff = 0)
		{
			var tileOrigin = Main.tile[x, y].Let(tile => new Point(x - tile.frameX / 18 % 3, y - tile.frameY / 18));
			var pos = (new Vector2(tileOrigin.X - fluff, tileOrigin.Y - fluff) * 16).ToPoint();
			var dims = (new Vector2(3 + fluff * 2, 2 + fluff * 2) * 16).ToPoint();
			return (new Rectangle(pos.X, pos.Y, dims.X, dims.Y), tileOrigin);
		}

		public static int ConsumeFromStack(int remaining, Item item, int index, bool goPoof = false, bool bounceLeftovers = false)
		{
			// Determine how many are coming off of this stack
			int taking = Math.Min(item.stack, remaining);

			// Update the stack
			item.stack -= taking;
			if (item.stack <= 0)
			{
				item.SetDefaults();
				item.active = false;

				if (goPoof)
					TSPlayer.All.SendData(PacketTypes.PoofOfSmoke, number: (int)item.Center.X, number2: (int)item.Center.Y);
			}
			else if (bounceLeftovers)
			{
				item.velocity.Y += Main.rand.Next(-25, -12) * 0.1f;
			}

			TSPlayer.All.SendData(PacketTypes.UpdateItemDrop, number: index);

			return remaining - taking;
		}

		public static int ConsumeAvailableItems(int remaining, IEnumerable<(Item item, int index)> items, bool goPoof = false, bool bounceLeftovers = false)
		{
			// Consume as many as we need
			foreach (var (item, index) in items)
			{
				remaining = ConsumeFromStack(remaining, item, index, goPoof, bounceLeftovers);

				// See if we're done
				if (remaining <= 0)
					break;
			}

			return remaining;
		}

		public static bool ConsumeItems(int count, IEnumerable<(Item item, int index)> items, bool goPoof = false, bool bounceLeftovers = false)
		{
			// Bail if we don't have enough to cover the whole amount
			if (items.Select(t => t.item.stack).Sum() < count)
				return false;

			// Consume the items
			ConsumeAvailableItems(count, items, goPoof, bounceLeftovers);
			return true;
		}

		public static Vector2 FindProbablySafeTeleportLocation(Point target)
		{
			// Look for a spot to teleport to
			var settings = new Player.RandomTeleportationAttemptSettings()
			{
				attemptsBeforeGivingUp = 1000,
				avoidAnyLiquid = false,
				avoidHurtTiles = true,
				avoidLava = true,
				avoidWalls = false,
				maximumFallDistanceFromOrignalPoint = 30,
				mostlySolidFloor = true
			};

			var destPos = Vector2.Zero;
			bool canSpawn = false;
			int rangeX = 100;
			int halfRangeX = rangeX / 2;
			int rangeY = 80;

			// TODO I'm not sure why CheckForGoodTeleportationSpot is an instance method, since it seems like the dimensions of a
			// Player object are always the same. Worth double-checking later, but in the meantime we'll just take a random player
			// and call it a day.
			Main.player.Where(p => p.active).FirstOrDefault()?.Let(player =>
			{
				destPos = player.CheckForGoodTeleportationSpot(ref canSpawn, target.X - halfRangeX, rangeX, target.Y, rangeY, settings);
				if (!canSpawn)
					destPos = player.CheckForGoodTeleportationSpot(ref canSpawn, target.X - rangeX, halfRangeX, target.Y, rangeY, settings);
				if (!canSpawn)
					destPos = player.CheckForGoodTeleportationSpot(ref canSpawn, target.X + halfRangeX, halfRangeX, target.Y, rangeY, settings);
			});


			if (canSpawn)
				return destPos;
			else
			{
				TShock.Log.Warn("Fell back to a random teleportation location; this shouldn't happen except very rarely");
				return new Vector2(target.X + Main.rand.NextFloatDirection() * rangeX, target.Y + Main.rand.NextFloatDirection() * rangeY);
			}
		}

		public static void DoFairyFX(Vector2 pos, int color = -1) => TSPlayer.All.SendData(PacketTypes.TreeGrowFX, null, 2, pos.X, pos.Y, color >= 0 ? color : Main.rand.Next(3));

		public static void RegisterChatCommands(string perm, params (string name, CommandDelegate action)[] commands)
		{
			foreach (var cmd in commands)
				TShockAPI.Commands.ChatCommands.Add(new Command(perm, cmd.action, cmd.name));
		}
	}
}
