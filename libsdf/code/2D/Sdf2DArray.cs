﻿using System;

namespace Sandbox.Sdf;

internal record struct Sdf2DArrayData( byte[] Samples, int BaseIndex, int RowStride )
{
	public byte this[int x, int y] => Samples[BaseIndex + x + y * RowStride];
}

public partial class Sdf2DArray : SdfArray<ISdf2D>
{
	public Sdf2DArray()
		: base( 2 )
	{
	}

	protected override Texture CreateTexture()
	{
		return new Texture2DBuilder()
			.WithFormat( ImageFormat.I8 )
			.WithSize( ArraySize, ArraySize )
			.WithData( Samples )
			.WithAnonymous( true )
			.Finish();
	}

	protected override void UpdateTexture( Texture texture )
	{
		texture.Update( Samples );
	}

	private (int MinX, int MinY, int MaxX, int MaxY) GetSampleRange( Rect bounds )
	{
		var (minX, maxX) = GetSampleRange( bounds.Left, bounds.Right );
		var (minY, maxY) = GetSampleRange( bounds.Top, bounds.Bottom );

		return (minX, minY, maxX, maxY);
	}

	public override bool Add<T>( in T sdf )
	{
		var (minX, minY, maxX, maxY) = GetSampleRange( sdf.Bounds );
		var maxDist = Quality.MaxDistance;

		var changed = false;

		for ( var y = minY; y < maxY; ++y )
		{
			var worldY = (y - Margin) * UnitSize;

			for ( int x = minX, index = minX + y * ArraySize; x < maxX; ++x, ++index )
			{
				var worldX = (x - Margin) * UnitSize;
				var sampled = sdf[new Vector2( worldX, worldY )];

				if ( sampled >= maxDist ) continue;

				var encoded = Encode( sampled );

				var oldValue = Samples[index];
				var newValue = Math.Min( encoded, oldValue );

				Samples[index] = newValue;

				changed |= oldValue != newValue;
			}
		}

		if ( changed )
		{
			MarkChanged();
		}

		return changed;
	}

	public override bool Subtract<T>( in T sdf )
	{
		var (minX, minY, maxX, maxY) = GetSampleRange( sdf.Bounds );
		var maxDist = Quality.MaxDistance;

		var changed = false;

		for ( var y = minY; y < maxY; ++y )
		{
			var worldY = (y - Margin) * UnitSize;

			for ( int x = minX, index = minX + y * ArraySize; x < maxX; ++x, ++index )
			{
				var worldX = (x - Margin) * UnitSize;
				var sampled = sdf[new Vector2( worldX, worldY )];

				if ( sampled >= maxDist ) continue;

				var encoded = Encode( sampled );

				var oldValue = Samples[index];
				var newValue = Math.Max( (byte)(MaxEncoded - encoded), oldValue );

				Samples[index] = newValue;

				changed |= oldValue != newValue;
			}
		}

		if ( changed )
		{
			MarkChanged();
		}

		return changed;
	}

	internal void WriteTo( Sdf2DMeshWriter writer, Sdf2DLayer layer, bool renderMesh, bool collisionMesh )
	{
		writer.Write( new Sdf2DArrayData( Samples, Margin * ArraySize + Margin, ArraySize ),
			layer, renderMesh, collisionMesh );
	}
}