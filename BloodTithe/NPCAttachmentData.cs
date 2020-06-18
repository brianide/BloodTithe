using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;

namespace BloodTithe
{
    class FairyExtendedIdle
    {
        public Vector2 HomePoint { get; set; }
        public int Timer { get; set; }
        public Action<TSPlayer> OnCollision { get; set; }
    }
}
