using System;
using System.Collections.Generic;

namespace Sandbox.Sdf;

partial class PolygonMeshBuilder
{
	private HashSet<(int A, int B)> PossibleCuts { get; } = new();

	[ThreadStatic]
	private static List<(int A, int B)> Bevel_PossibleCutList;

	[ThreadStatic]
	private static List<int> Bevel_ActiveEdgeList;

	public bool Debug { get; set; }

	public void Bevel( float width, float height, bool smooth )
	{
		if ( width < 0f )
		{
			throw new ArgumentOutOfRangeException( nameof(width) );
		}

		var cutList = Bevel_PossibleCutList ??= new List<(int A, int B)>();
		var edgeList = Bevel_ActiveEdgeList ??= new List<int>();

		_nextDistance = _prevDistance + width;
		_nextHeight = _prevHeight + height;
		_nextAngle = MathF.Atan2( width, height );
		_minSmoothNormalDot = MathF.Cos( MathF.PI / 180f * MaxSmoothAngle );

		_invDistance = width <= 0.0001f ? 0f : 1f / (_nextDistance - _prevDistance);

		if ( !smooth )
		{
			_prevAngle = _nextAngle;

			foreach ( var index in _activeEdges )
			{
				ref var edge = ref _allEdges[index];
				edge.Vertices = (-1, -1);
			}
		}
		else if ( float.IsPositiveInfinity( width ) )
		{
			_prevAngle = _nextAngle;
			BlendNormals( _prevPrevHeight, _prevHeight, _prevPrevAngle, _prevAngle );
		}
		else if ( _prevDistance > 0f)
		{
			_prevAngle = (_prevAngle + _nextAngle) * 0.5f;
			BlendNormals( _prevPrevHeight, _prevHeight, _prevPrevAngle, _prevAngle );
		}

		PossibleCuts.Clear();

		foreach ( var index in _activeEdges )
		{
			ref var edge = ref _allEdges[index];
			UpdateMaxDistance( ref edge, _allEdges[edge.NextEdge] );

			if ( Vector2.Dot( edge.Tangent, edge.Velocity ) > 0.001f ) continue;

			foreach ( var otherIndex in _activeEdges )
			{
				if ( otherIndex != index )
				{
					PossibleCuts.Add( (index, otherIndex) );
				}
			}
		}

		if ( MathF.Abs( _nextDistance ) > 0.001f )
		{
			var maxIterations = _activeEdges.Count * _activeEdges.Count;

			int iterations;
			for ( iterations = 0; iterations < maxIterations && _activeEdges.Count > 0; ++iterations )
			{
				int? closedEdge = null;
				int? splitEdge = null;
				int? splittingEdge = null;

				Vector2 bestClosePos = default;
				Vector2 bestSplitPos = default;

				var bestDist = _nextDistance;

				foreach ( var index in _activeEdges )
				{
					var edge = _allEdges[index];

					if ( edge.MaxDistance >= bestDist ) continue;

					bestDist = edge.MaxDistance;
					closedEdge = edge.Index;
					bestClosePos = edge.Project( edge.MaxDistance );

					splitEdge = splittingEdge = null;
				}

				cutList.Clear();
				cutList.AddRange( PossibleCuts );

				foreach ( var (index, otherIndex) in cutList )
				{
					if ( !_activeEdges.Contains( index ) || !_activeEdges.Contains( otherIndex ) )
					{
						PossibleCuts.Remove( (index, otherIndex) );
						continue;
					}

					var edge = _allEdges[index];
					var other = _allEdges[otherIndex];

					var splitDist = CalculateSplitDistance( edge, other, _allEdges[other.NextEdge], out var splitPos );

					if ( float.IsPositiveInfinity( splitDist ) )
					{
						PossibleCuts.Remove( (index, otherIndex) );
						continue;
					}

					if ( splitDist >= bestDist ) continue;

					bestDist = splitDist;
					bestSplitPos = splitPos;

					closedEdge = null;
					splitEdge = other.Index;
					splittingEdge = edge.Index;
				}

				if ( Debug )
				{
					Log.Info( $"{iterations}: {_activeEdges.Count}, {bestDist:R}, ({splittingEdge}, {splitEdge}, {closedEdge})" );
				}

				if ( splittingEdge != null )
				{
					EnsureCapacity( 2 );

					ref var a = ref _allEdges[splitEdge.Value];
					ref var d = ref _allEdges[splittingEdge.Value];
					ref var b = ref _allEdges[AddEdge( bestSplitPos, a.Tangent, bestDist )];
					ref var c = ref _allEdges[d.PrevEdge];
					ref var e = ref _allEdges[AddEdge( bestSplitPos, d.Tangent, bestDist )];
					ref var aNext = ref _allEdges[a.NextEdge];
					ref var dNext = ref _allEdges[d.NextEdge];

					var ai = AddVertices( ref a ).Next;
					var fi = AddVertices( ref aNext ).Prev;
					var ci = AddVertices( ref c ).Next;
					var di = AddVertices( ref d );
					var gi = AddVertices( ref dNext ).Prev;

					_activeEdges.Remove( d.Index );
					_activeEdges.Add( b.Index );
					_activeEdges.Add( e.Index );

					ConnectEdges( ref a, ref e );
					ConnectEdges( ref e, ref dNext );

					ConnectEdges( ref c, ref b );
					ConnectEdges( ref b, ref aNext );

					UpdateMaxDistance( ref a, e );
					UpdateMaxDistance( ref e, dNext );
					UpdateMaxDistance( ref dNext, _allEdges[dNext.NextEdge] );

					UpdateMaxDistance( ref c, b );
					UpdateMaxDistance( ref b, aNext );
					UpdateMaxDistance( ref aNext, _allEdges[aNext.NextEdge] );

					var bi = AddVertices( ref b );
					var ei = AddVertices( ref e );

					AddTriangle( ai, bi.Next, fi );
					AddTriangle( ci, bi.Prev, di.Prev );
					AddTriangle( di.Next, ei.Next, gi );

					AddAllPossibleCuts( b );
					AddAllPossibleCuts( e );
					AddAllPossibleCuts( dNext );
					AddAllPossibleCuts( aNext );

					continue;
				}

				if ( closedEdge != null )
				{
					EnsureCapacity( 1 );

					ref var b = ref _allEdges[closedEdge.Value];
					ref var a = ref _allEdges[b.PrevEdge];
					ref var c = ref _allEdges[b.NextEdge];
					ref var cNext = ref _allEdges[c.NextEdge];
					ref var d = ref _allEdges[AddEdge( bestClosePos, c.Tangent, bestDist )];

					_activeEdges.Remove( b.Index );
					_activeEdges.Remove( c.Index );

					if ( b.PrevEdge == b.NextEdge )
					{
						continue;
					}

					_activeEdges.Add( d.Index );

					ConnectEdges( ref a, ref d );
					ConnectEdges( ref d, ref cNext );

					UpdateMaxDistance( ref a, d );
					UpdateMaxDistance( ref d, cNext );
					UpdateMaxDistance( ref cNext, _allEdges[cNext.NextEdge] );

					var ai = AddVertices( ref a );
					var bi = AddVertices( ref b );
					var ci = AddVertices( ref c );
					var ei = AddVertices( ref cNext );
					var di = AddVertices( ref d );

					var fi = _vertices.Count;

					_vertices.Add( _vertices[di.Prev] );
					_normals.Add( _normals[bi.Next] );

					AddTriangle( ai.Next, di.Prev, bi.Prev );
					AddTriangle( bi.Next, fi, ci.Prev );
					AddTriangle( ci.Next, di.Next, ei.Prev );

					AddAllPossibleCuts( d );
					AddAllPossibleCuts( cNext );

					continue;
				}

				break;
			}

			if ( _activeEdges.Count > 0 && iterations == maxIterations )
			{
				throw new Exception( $"Exploded after {iterations} with {_activeEdges.Count} active edges!" );
			}
		}

		EnsureCapacity( _activeEdges.Count );

		edgeList.Clear();
		edgeList.AddRange( _activeEdges );

		_activeEdges.Clear();

		foreach ( var index in edgeList )
		{
			ref var b = ref _allEdges[index];
			ref var a = ref _allEdges[b.PrevEdge];
			ref var c = ref _allEdges[b.NextEdge];
			ref var d = ref _allEdges[AddEdge( b.Project( _nextDistance ), b.Tangent, _nextDistance )];

			var ai = AddVertices( ref a );
			var bi = AddVertices( ref b );
			var ci = AddVertices( ref c );

			ConnectEdges( ref a, ref d );
			ConnectEdges( ref d, ref c );

			var di = AddVertices( ref d, true );

			AddTriangle( ai.Next, di.Prev, bi.Prev );
			AddTriangle( bi.Next, di.Next, ci.Prev );

			_activeEdges.Add( d.Index );
		}

		_prevDistance = _nextDistance;
		_prevPrevHeight = _prevHeight;
		_prevHeight = _nextHeight;
		_prevPrevAngle = _prevAngle;
		_prevAngle = _nextAngle;
	}

	private void AddAllPossibleCuts( in Edge edge )
	{
		foreach ( var otherIndex in _activeEdges )
		{
			if ( otherIndex == edge.Index ) continue;

			PossibleCuts.Add( (edge.Index, otherIndex) );
			PossibleCuts.Add( (otherIndex, edge.Index) );
		}
	}

	private static void UpdateMaxDistance( ref Edge edge, in Edge nextEdge )
	{
		if ( edge.NextEdge == edge.PrevEdge )
		{
			edge.MaxDistance = edge.Distance;
			return;
		}

		var baseDistance = Math.Max( edge.Distance, nextEdge.Distance );
		var thisOrigin = edge.Project( baseDistance );
		var nextOrigin = nextEdge.Project( baseDistance );

		var posDist = Vector2.Dot( nextOrigin - thisOrigin, edge.Tangent );
		var epsilon = Helpers.GetEpsilon( thisOrigin, nextOrigin );

		var dPrev = Vector2.Dot( edge.Velocity, edge.Tangent );
		var dNext = Vector2.Dot( nextEdge.Velocity, edge.Tangent );

		if ( dPrev - dNext <= 0.001f )
		{
			edge.MaxDistance = posDist <= epsilon ? baseDistance : float.PositiveInfinity;
		}
		else
		{
			edge.MaxDistance = baseDistance + MathF.Max( 0f, posDist / (dPrev - dNext) );
		}
	}

	private static void ConnectEdges( ref Edge prev, ref Edge next )
	{
		prev.NextEdge = next.Index;
		next.PrevEdge = prev.Index;

		var sum = prev.Normal + next.Normal;
		var sqrMag = sum.LengthSquared;

		if ( sqrMag < 0.00001f )
		{
			next.Velocity = Vector2.Zero;
		}
		else
		{
			next.Velocity = 2f * sum / sum.LengthSquared;
		}
	}

	private static float CalculateSplitDistance( in Edge edge, in Edge other, in Edge otherNext, out Vector2 splitPos )
	{
		splitPos = default;

		if ( other.Index == edge.Index || edge.Velocity.LengthSquared <= 0f )
		{
			return float.PositiveInfinity;
		}

		var dv = Vector2.Dot( other.Velocity - edge.Velocity, other.Normal );

		if ( dv <= Helpers.GetEpsilon( edge.Velocity, other.Velocity ) )
		{
			return float.PositiveInfinity;
		}

		var baseDistance = Math.Max( edge.Distance, Math.Max( other.Distance, otherNext.Distance ) );
		var thisOrigin = edge.Project( baseDistance );
		var edgeOrigin = other.Project( baseDistance );

		var dx = Vector2.Dot( thisOrigin - edgeOrigin, other.Normal );

		if ( dx <= -Helpers.GetEpsilon( thisOrigin, edgeOrigin ) )
		{
			return float.PositiveInfinity;
		}

		var t = dx / dv;

		if ( t < 0f )
		{
			return float.PositiveInfinity;
		}

		if ( baseDistance + t >= edge.MaxDistance || baseDistance + t >= other.MaxDistance )
		{
			return float.PositiveInfinity;
		}

		splitPos = thisOrigin + edge.Velocity * t;

		var prevPos = edgeOrigin + other.Velocity * t;
		var nextPos = otherNext.Project( baseDistance + t );

		var dPrev = Vector2.Dot( splitPos - prevPos, other.Tangent );
		var dNext = Vector2.Dot( splitPos - nextPos, other.Tangent );

		var epsilon = Helpers.GetEpsilon( prevPos, nextPos );

		if ( dPrev <= epsilon || dNext >= -epsilon )
		{
			return float.PositiveInfinity;
		}

		return baseDistance + t;
	}
}
