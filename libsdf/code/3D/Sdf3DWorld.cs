﻿using System;
using System.Collections.Generic;

namespace Sandbox.Sdf;

/// <summary>
/// Main entity for creating a 3D surface that can be added to and subtracted from.
/// Multiple volumes can be added to this entity with different materials.
/// </summary>
public partial class Sdf3DWorld : SdfWorld<Sdf3DWorld, Sdf3DChunk, Sdf3DVolume, (int X, int Y, int Z), Sdf3DArray, ISdf3D>
{
	internal Sdf3DWorld( ISdfWorldImpl impl )
		: base( impl )
	{

	}

	/// <summary>
	/// Create a <see cref="SceneObject"/>-based 3D surface that can be added to and subtracted from.
	/// This won't use any entities, so is safe for use in menus and editors.
	/// </summary>
	public Sdf3DWorld( SceneWorld sceneWorld )
		: base( sceneWorld )
	{

	}

	/// <summary>
	/// Create an <see cref="Entity"/>-based 3D surface that can be added to and subtracted from.
	/// See <see cref="Sdf3DWorld(SceneWorld)"/> for creating worlds in menus and editors.
	/// </summary>
	public Sdf3DWorld()
	{

	}

	public override int Dimensions => 3;

	private ((int X, int Y, int Z) Min, (int X, int Y, int Z) Max) GetChunkRange( BBox bounds, WorldQuality quality )
	{
		var unitSize = quality.UnitSize;

		var min = (bounds.Mins - quality.MaxDistance - unitSize) / quality.ChunkSize;
		var max = (bounds.Maxs + quality.MaxDistance + unitSize) / quality.ChunkSize;

		var minX = (int) MathF.Floor( min.x );
		var minY = (int) MathF.Floor( min.y );
		var minZ = (int) MathF.Floor( min.z );

		var maxX = (int) MathF.Ceiling( max.x );
		var maxY = (int) MathF.Ceiling( max.y );
		var maxZ = (int) MathF.Ceiling( max.z );

		return ((minX, minY, minZ), (maxX, maxY, maxZ));
	}

	private IEnumerable<(int X, int Y, int Z)> GetChunks( BBox bounds, WorldQuality quality )
	{
		var ((minX, minY, minZ), (maxX, maxY, maxZ)) = GetChunkRange( bounds, quality );

		for ( var z = minZ; z < maxZ; ++z )
		for ( var y = minY; y < maxY; ++y )
		for ( var x = minX; x < maxX; ++x )
		{
			yield return (x, y, z);
		}
	}

	/// <inheritdoc />
	protected override IEnumerable<(int X, int Y, int Z)> GetAffectedChunks<T>( T sdf, WorldQuality quality )
	{
		if ( sdf.Bounds is { } bounds )
		{
			return GetChunks( bounds, quality );
		}

		throw new Exception( "Can only make modifications with an SDF with Bounds != null" );
	}
}
