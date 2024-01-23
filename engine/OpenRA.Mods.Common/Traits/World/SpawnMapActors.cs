#region Copyright & License Information
/*
 * Copyright 2007-2022 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Spawns the initial units for each player upon game start.")]
	public class SpawnMapActorsInfo : TraitInfo<SpawnMapActors> { }

	public class SpawnMapActors : IWorldLoaded
	{
		public Dictionary<string, Actor> Actors = new Dictionary<string, Actor>();
		public uint LastMapActorID { get; private set; }

		public void WorldLoaded(World world, WorldRenderer wr)
		{
			var preventMapSpawns = world.WorldActor.TraitsImplementing<IPreventMapSpawn>()
				.Concat(world.WorldActor.Owner.PlayerActor.TraitsImplementing<IPreventMapSpawn>())
				.ToArray();

			foreach (var kv in world.Map.ActorDefinitions)
			{
				var actorReference = new ActorReference(kv.Value.Value, kv.Value.ToDictionary());

				// If an actor's doesn't have a valid owner transfer ownership to neutral
				var ownerInit = actorReference.Get<OwnerInit>();
				if (!world.Players.Any(p => p.InternalName == ownerInit.InternalName))
				{
					actorReference.Remove(ownerInit);
					actorReference.Add(new OwnerInit(world.WorldActor.Owner));
				}

				actorReference.Add(new SkipMakeAnimsInit());
				actorReference.Add(new SpawnedByMapInit(kv.Key));

				if (PreventMapSpawn(world, actorReference, preventMapSpawns))
					continue;

				var actor = world.CreateActor(true, actorReference);
				Actors[kv.Key] = actor;
				LastMapActorID = actor.ActorID;
			}

			/// Cast Shadows of spawned units
			// TODO: If fog enabled...

			// world.ActorMap.Update();
			SetShadows(world);
		}

		public static void SetShadows(World world)
		{
			world.ActorMap.TickFunction();

			var map = world.Map;

			if (map.Visibility.HasFlag(MapVisibility.Shellmap))
				return;

			var ShadowLayers = new CellLayer<CellLayer<byte>>(map);

			foreach (var fromUV in map.AllCells.MapCoords)
			{
				var shadowLayer = new CellLayer<byte>(map);

				foreach (var tilePos in map.FindTilesInAnnulus(fromUV.ToCPos(map), 24, 25, false)) // TODO 25/25 works? Test later
				{
					MPos toUV = tilePos.ToMPos(map);

					if (BlocksSight.AnyBlockingActorsBetween(world, fromUV.ToWPos(map), toUV.ToWPos(map), new WDist(1), out WPos hit, null, false))
					{
						shadowLayer[toUV] = (byte)1;
					}
					else
					{
						shadowLayer[toUV] = (byte)2;
					}
				}

				ShadowLayers[fromUV] = shadowLayer;
			}

			map.ShadowLayers = ShadowLayers;
		}

		bool PreventMapSpawn(World world, ActorReference actorReference, IEnumerable<IPreventMapSpawn> preventMapSpawns)
		{
			foreach (var pms in preventMapSpawns)
				if (pms.PreventMapSpawn(world, actorReference))
					return true;

			return false;
		}
	}

	public class SkipMakeAnimsInit : RuntimeFlagInit { }
	public class SpawnedByMapInit : ValueActorInit<string>, ISuppressInitExport, ISingleInstanceInit
	{
		public SpawnedByMapInit(string value)
			: base(value) { }
	}
}
