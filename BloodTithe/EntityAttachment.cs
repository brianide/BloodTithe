using Microsoft.Xna.Framework;
using System;
using System.Linq;
using Terraria;
using TShockAPI;

namespace BloodTithe
{
	abstract class EntityAttachment
    {
		private int timer;
        public bool Update() => OnUpdate(timer++);
		protected abstract bool OnUpdate(int tick);
	}

    class FairyExtendedIdle : EntityAttachment
    {
		protected NPC Fairy { get; }
		public Vector2 HomePoint { get; }
        public Action<TSPlayer> OnCollision { get; }

		public FairyExtendedIdle(NPC entity, Vector2 homePoint, Action<TSPlayer> onCollision)
		{
			Fairy = entity;
			HomePoint = homePoint;
			OnCollision = onCollision;
		}

		protected override bool OnUpdate(int tick)
        {
			// Detach special handling if the fairy's five-minute timer is up
			if (Fairy.ai[2] == 7)
				return false;

			// Prevent despawning
			if (Fairy.ai[3] == 200)
				Fairy.ai[3] = 16;

			// Move toward the home point periodically (or when the player manages to hit us) so that
			// we don't get too far out of position
			if (tick % 360 == 0 || Fairy.life < Fairy.lifeMax)
			{
				Fairy.ai[3] = -15;
				Fairy.velocity = (HomePoint - Fairy.Center) * 0.1f;
				Fairy.life = Fairy.lifeMax;
				Fairy.netUpdate = true;
			}

			// On collision, run the handler and then detach the special handling. We wait until the AI
			// has run for a second before we actually start trying to collide.
			if (OnCollision is object && tick > 60 && tick % 6 == 0)
			{
				var colliding = TShock.Players.Where(p => p is object && p.ConnectionAlive && p.TPlayer.Hitbox.Contains(HomePoint.ToPoint()));
				if (colliding.Any())
				{
					colliding.ForEach(OnCollision);
					return false;
				}

			}

			return true;
		}
    }
}
