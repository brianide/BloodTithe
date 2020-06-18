﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using OTAPI.Tile;
using System.Text;
using System.Threading.Tasks;
using Terraria.ID;
using TShockAPI;
using Terraria;
using Terraria.Utilities;
using System.Runtime.CompilerServices;

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

		public static bool ConsumeItems(int count, IEnumerable<(Item item, int index)> items, bool bounceLeftovers = false)
		{
			// Determine if we have enough
			if (items.Select(t => t.item.stack).Sum() < count)
				return false;

			// Consume as many as we need
			int remaining = count;
			foreach (var (item, index) in items)
			{
				// Determine how many are coming off of this stack
				int taking = Math.Min(item.stack, remaining);

				// Update the stack
				item.stack -= taking;
				if (item.stack <= 0)
				{
					item.SetDefaults();
					item.active = false;
				}
				else if (bounceLeftovers)
				{
					item.velocity.Y += Main.rand.Next(-25, -12) * 0.1f;
				}

				TSPlayer.All.SendData(PacketTypes.UpdateItemDrop, number: index);

				// See if we're done
				remaining -= taking;
				if (remaining <= 0)
					break;
			}

			return true;
		}

		public static Vector2 FindProbablySafeTeleportLocation(TSPlayer player, Point target)
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

			bool canSpawn = false;
			int rangeX = 100;
			int halfRangeX = rangeX / 2;
			int rangeY = 80;

			var destPos = player.TPlayer.CheckForGoodTeleportationSpot(ref canSpawn, target.X - halfRangeX, rangeX, target.Y, rangeY, settings);
			if (!canSpawn)
				destPos = player.TPlayer.CheckForGoodTeleportationSpot(ref canSpawn, target.X - rangeX, halfRangeX, target.Y, rangeY, settings);
			if (!canSpawn)
				destPos = player.TPlayer.CheckForGoodTeleportationSpot(ref canSpawn, target.X + halfRangeX, halfRangeX, target.Y, rangeY, settings);

			// All 3000 attempts failed, so fuck it; drop 'em wherever
			if (!canSpawn)
				destPos = new Vector2(target.X + Main.rand.NextFloatDirection() * rangeX, target.Y + Main.rand.NextFloatDirection() * rangeY);

			return destPos;
		}

		public static void DoFairyFX(Vector2 pos, int color = -1) => TSPlayer.All.SendData(PacketTypes.TreeGrowFX, null, 2, pos.X, pos.Y, color >= 0 ? color : Main.rand.Next(3));

		public static void RegisterChatCommands(string perm, params (string name, CommandDelegate action)[] commands)
		{
			foreach (var cmd in commands)
				TShockAPI.Commands.ChatCommands.Add(new Command(perm, cmd.action, cmd.name));
		}
	}
}
