/* 
 * Copyright (c) 2003-2005, University of Maryland
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without modification, are permitted provided
 * that the following conditions are met:
 * 
 *		Redistributions of source code must retain the above copyright notice, this list of conditions
 *		and the following disclaimer.
 * 
 *		Redistributions in binary form must reproduce the above copyright notice, this list of conditions
 *		and the following disclaimer in the documentation and/or other materials provided with the
 *		distribution.
 * 
 *		Neither the name of the University of Maryland nor the names of its contributors may be used to
 *		endorse or promote products derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED
 * WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
 * PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
 * ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
 * LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR
 * TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
 * ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 * 
 * Piccolo was written at the Human-Computer Interaction Laboratory www.cs.umd.edu/hcil by Jesse Grosjean
 * and ported to C# by Aaron Clamage under the supervision of Ben Bederson.  The Piccolo website is
 * www.cs.umd.edu/hcil/piccolo.
 */

using System;
using System.Runtime.Serialization;
using System.Text;

using XnaPiccolo;
using XnaPiccolo.Util;
using Xna2dEditor;
using Microsoft.Xna.Framework;

namespace XnaPiccolo.Nodes 
{
	/// <summary>
	/// <b>PPath</b> is a wrapper around a <see cref="System.Drawing.Drawing2D.GraphicsPath">
	/// System.Drawing.Drawing2D.GraphicsPath</see>.
	/// </summary>
	/// <remarks>
	/// The SetBounds method works by scaling the path to fit into the specified bounds.
	/// This normally works well, but if the specified base bounds get too small then it is
	/// impossible to expand the path shape again since all its numbers have tended to zero,
	/// so application code may need to take this into consideration. 
	/// <para>
	/// One option that applications have is to call <c>StartResizeBounds</c> before starting
	/// an interaction that may make the bounds very small, and calling <c>EndResizeBounds</c>
	/// when this interaction is finished. When this is done PPath will use a copy of the
	/// original path to do the resizing so the numbers in the path wont loose resolution.
	/// </para>
	/// <para>
	/// This class also provides methods for constructing common shapes using a GraphicsPath.
	/// </para>
	/// <para>
	/// <b>Performance Note</b>:  Checking for intersections between some paths and rectangles
	/// can be very slow.  This is due to the way .NET implements the IsVisible method.
	/// The problem generally occurs in extreme cases, when the path consists of numerous
	/// lines joined at very steep angles, which exhausts the intersection algorithm.
	/// One simple workaround is to break the figure up into several PPath nodes.  Also,
	/// remember to set the Brush to null if you do not want to a fill a path.  Otherwise,
	/// the path will be filled with a white brush, and picking will be more expensive.
	/// </para>
	/// </remarks>
	[Serializable]
	public class PPath : PNode, ISerializable 
    {
		#region Fields
		/// <summary>
		/// The key that identifies a change in this node's <see cref="Pen">Pen</see>.
		/// </summary>
		/// <remarks>
		/// In a property change event both the old and new value will be set correctly
		/// to Pen objects.
		/// </remarks>
		protected static readonly object PROPERTY_KEY_PEN = new object();

		/// <summary>
		/// A bit field that identifies a <see cref="PenChanged">PenChanged</see> event.
		/// </summary>
		/// <remarks>
		/// This field is used to indicate whether PenChanged events should be forwarded to
		/// a node's parent.
		/// <seealso cref="XnaPiccolo.Event.PPropertyEventArgs">PPropertyEventArgs</seealso>.
		/// <seealso cref="XnaPiccolo.PNode.PropertyChangeParentMask">PropertyChangeParentMask</seealso>.
		/// </remarks>
		public const int PROPERTY_CODE_PEN = 1 << 15;

		/// <summary>
		/// The key that identifies a change in this node's <see cref="PathReference">Path</see>.
		/// </summary>
		/// <remarks>
		/// In a property change event the new value will be a reference to this node's path, but old
		/// value will always be null.
		/// </remarks>
		protected static readonly object PROPERTY_KEY_PATH = new object();

		/// <summary>
		/// A bit field that identifies a <see cref="PathChanged">PathChanged</see> event.
		/// </summary>
		/// <remarks>
		/// This field is used to indicate whether PathChanged events should be forwarded to
		/// a node's parent.
		/// <seealso cref="XnaPiccolo.Event.PPropertyEventArgs">PPropertyEventArgs</seealso>.
		/// <seealso cref="XnaPiccolo.PNode.PropertyChangeParentMask">PropertyChangeParentMask</seealso>.
		/// </remarks>
		public const int PROPERTY_CODE_PATH = 1 << 16;

		private static XnaGraphicsPath TEMP_PATH = new XnaGraphicsPath();

		private static RectangleFx TEMP_REGION = new RectangleFx();
		private static Matrix TEMP_MATRIX = Matrix.Identity;
		private static System.Drawing.Pen DEFAULT_PEN = System.Drawing.Pens.Black;
		private const System.Drawing.Drawing2D.FillMode DEFAULT_FILLMODE = System.Drawing.Drawing2D.FillMode.Alternate;
	
		private XnaGraphicsPath path;
		[NonSerialized] private XnaGraphicsPath resizePath;
		[NonSerialized] private System.Drawing.Pen pen;
		[NonSerialized] private bool updatingBoundsFromPath;
		private PathPickMode pickMode = PathPickMode.Fast;
		#endregion

		#region Enums
		/// <summary>
		/// Represents the types of picking modes for a PPath object.
		/// </summary>
		public enum PathPickMode {
			/// <summary>
			/// Faster Picking.  Paths are picked in local coordinates.
			/// </summary>
			Fast,

			/// <summary>
			/// Slower and more accurate picking.  Paths are picked in canvas
			/// coordinates.
			/// </summary>
			Accurate
		}
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PPath with an empty path.
		/// </summary>
		public PPath() 
        {
			pen = DEFAULT_PEN;
			path = new XnaGraphicsPath();
		}

		/// <summary>
		/// Constructs a new PPath wrapping the given
		/// <see cref="System.Drawing.Drawing2D.GraphicsPath">
		/// System.Drawing.Drawing2D.GraphicsPath</see>.
		/// </summary>
		/// <param name="path">The path to wrap.</param>
		public PPath(XnaGraphicsPath path) 
        {
			pen = DEFAULT_PEN;
			this.path = (XnaGraphicsPath)path.Clone();
			UpdateBoundsFromPath();
		}

		/// <summary>
		/// Constructs a new PPath with the given points and point types.
		/// </summary>
		/// <param name="pts">The points in the path.</param>
		/// <param name="types">The types of the points in the path.</param>
		public PPath(PointFx[] pts, PathPointType[] types)
			: this(pts, types, DEFAULT_PEN) 
        {
		}

		/// <summary>
		/// Constructs a new PPath with the given points, point types and pen.
		/// </summary>
		/// <param name="pts">The points in the path.</param>
		/// <param name="types">The types of the points in the path.</param>
		/// <param name="pen">The pen to use when rendering this node.</param>
		public PPath(PointFx[] pts, PathPointType[] types, System.Drawing.Pen pen)
			: this(pts, types, DEFAULT_FILLMODE, pen) 
        {
		}

		/// <summary>
		/// Constructs a new PPath with the given points, point types and fill mode.
		/// </summary>
		/// <param name="pts">The points in the path.</param>
		/// <param name="types">The types of the points in the path.</param>
		/// <param name="fillMode">The fill mode to use when rendering this node.</param>
		public PPath(PointFx[] pts, PathPointType[] types, System.Drawing.Drawing2D.FillMode fillMode)
			: this(pts, types, fillMode, DEFAULT_PEN) 
        {
		}

		/// <summary>
		/// Constructs a new PPath with the given points, point types, fill mode and pen.
		/// </summary>
		/// <param name="pts">The points in the path.</param>
		/// <param name="types">The types of the points in the path.</param>
		/// <param name="fillMode">The fill mode to use when rendering this node.</param>
		/// <param name="pen">The pen to use when rendering this node.</param>
		public PPath(PointFx[] pts, PathPointType[] types, System.Drawing.Drawing2D.FillMode fillMode, System.Drawing.Pen pen) 
        {
			path = new XnaGraphicsPath(pts, types, fillMode);
			this.pen = pen;
			UpdateBoundsFromPath();
		}
		#endregion

		#region Basic Shapes
		//****************************************************************
		// Basic Shapes - Methods used for creating common paths.
		//****************************************************************

		/// <summary>
		/// Creates a new PPath with the shape of a line specified by the given coordinates.
		/// </summary>
		/// <param name="x1">The x-coordinate of the start-point of the line.</param>
		/// <param name="y1">The y-coordinate of the start-point of the line.</param>
		/// <param name="x2">The x-coordinate of the end-point of the line.</param>
		/// <param name="y2">The y-coordinate of the end-point of the line.</param>
		/// <returns>The new PPath node.</returns>
		public static PPath CreateLine(float x1, float y1, float x2, float y2) 
        {
			PPath result = new PPath();
			result.AddLine(x1, y1, x2, y2);
			return result;
		}

		/// <summary>
		/// Creates a new PPath with the shape of the rectangle specified by the given dimensions.
		/// </summary>
		/// <param name="x">The x-coordinate of the top left corner of the rectangle.</param>
		/// <param name="y">The y-coordinate of the top left corner of the rectangle.</param>
		/// <param name="width">The width of the rectangle.</param>
		/// <param name="height">The height of the rectangle.</param>
		/// <returns>The new PPath node.</returns>
		public static PPath CreateRectangle(float x, float y, float width, float height) 
        {
			PPath result = new PPath();
			result.AddRectangle(x, y, width, height);
			result.Brush = Color.White;
			return result;
		}

		/// <summary>
		/// Creates a new PPath with the shape of the ellipse specified by the given dimensions.
		/// </summary>
		/// <param name="x">
		/// The x-coordinate of the top left corner of the bounding box of the ellipse.
		/// </param>
		/// <param name="y">
		/// The y-coordinate of the top left corner of the bounding box of the ellipse.
		/// </param>
		/// <param name="width">The width of the ellipse.</param>
		/// <param name="height">The height of the ellipse.</param>
		/// <returns>The new PPath node.</returns>
		public static PPath CreateEllipse(float x, float y, float width, float height) 
        {
			PPath result = new PPath();
			result.AddEllipse(x, y, width, height);
			result.Brush = Color.White;
			return result;
		}

		/// <summary>
		/// Creates a new PPath with the shape of the polygon specified by the given dimension.
		/// </summary>
		/// <param name="points">The points in the desired polygon.</param>
		/// <returns>The new PPath node.</returns>
		public static PPath CreatePolygon(PointFx[] points) 
        {
			PPath result = new PPath();
			result.AddPolygon(points);
			result.Brush = Color.White;
			return result;
		}
		#endregion

		#region Pen
		//****************************************************************
		// Pen - Methods for changing the pen used when rendering the
		// PPath.
		//****************************************************************

		/// <summary>
		/// Occurs when there is a change in this node's <see cref="Pen">Pen</see>.
		/// </summary>
		/// <remarks>
		/// When a user attaches an event handler to the PenChanged Event as in
		/// PenChanged += new PPropertyEventHandler(aHandler),
		/// the add method adds the handler to the delegate for the event
		/// (keyed by PROPERTY_KEY_PEN in the Events list).
		/// When a user removes an event handler from the PenChanged event as in 
		/// PenChanged -= new PPropertyEventHandler(aHandler),
		/// the remove method removes the handler from the delegate for the event
		/// (keyed by PROPERTY_KEY_PEN in the Events list).
		/// </remarks>
		public virtual event PPropertyEventHandler PenChanged 
        {
			add { HandlerList.AddHandler(PROPERTY_KEY_PEN, value); }
			remove { HandlerList.RemoveHandler(PROPERTY_KEY_PEN, value); }
		}

		/// <summary>
		/// Gets or sets the pen used when rendering this node.
		/// </summary>
		/// <value>The pen used when rendering this node.</value>
		public virtual System.Drawing.Pen Pen 
        {
			get { return pen; }
			set 
            {
				System.Drawing.Pen old = pen;
				pen = value;
				UpdateBoundsFromPath();
				InvalidatePaint();
				FirePropertyChangedEvent(PROPERTY_KEY_PEN, PROPERTY_CODE_PEN, old, pen);
			}
		}
		#endregion

		#region Picking Mode
		/// <summary>
		/// Gets or sets the mode used to pick this node.
		/// <seealso cref="PathPickMode">PathPickMode</seealso>
		/// </summary>
		/// <value>The mode used to pick this node.</value>
		public virtual PathPickMode PickMode {
			get { return pickMode; }
			set {
				this.pickMode = value;
			}
		}
		#endregion

		#region Bounds
		//****************************************************************
		// Bounds - Methods for manipulating/updating the bounds of a
		// PPath.
		//****************************************************************

		/// <summary>
		/// Overridden.  See <see cref="PNode.StartResizeBounds">PNode.StartResizeBounds</see>.
		/// </summary>
		public override void StartResizeBounds() 
        {
			resizePath = new XnaGraphicsPath();
			resizePath.AddPath(path, false);
		}

		/// <summary>
		/// Overridden.  See <see cref="PNode.EndResizeBounds">PNode.EndResizeBounds</see>.
		/// </summary>
		public override void EndResizeBounds() 
        {
			resizePath = null;
	 	}

		/// <summary>
		/// Overridden.  Set the bounds of this path.
		/// </summary>
		/// <param name="x">The new x-coordinate of the bounds/</param>
		/// <param name="y">The new y-coordinate of the bounds.</param>
		/// <param name="width">The new width of the bounds.</param>
		/// <param name="height">The new height of the bounds.</param>
		/// <returns>True if the bounds have changed; otherwise, false.</returns>
		/// <remarks>
		/// This works by scaling the path to fit into the specified bounds.  This normally
		/// works well, but if the specified base bounds get too small then it is impossible
		/// to expand the path shape again since all its numbers have tended to zero, so
		/// application code may need to take this into consideration.
		/// </remarks>
		protected override void InternalUpdateBounds(float x, float y, float width, float height) 
        {
			if (updatingBoundsFromPath || path == null) 
            {
				return;
			}

			if (resizePath != null) 
            {
				path.Reset();
				path.AddPath(resizePath, false);
			}

            RectangleFx pathBounds = path.GetBounds();

			if (pen != null && path.PointCount > 0) 
            {
				try 
                {
					TEMP_PATH.Reset();
					TEMP_PATH.AddPath(path, false);

					TEMP_PATH.Widen(pen);
                    RectangleFx penPathBounds = TEMP_PATH.GetBounds();

					float strokeOutset = Math.Max(penPathBounds.Width - pathBounds.Width, 
						penPathBounds.Height - pathBounds.Height);

					x += strokeOutset / 2;
					y += strokeOutset / 2;
					width -= strokeOutset;
					height -= strokeOutset;
				} 
                catch (OutOfMemoryException) 
                {
					// Catch the case where the path is a single point
				}
			}

			float scaleX = (width == 0 || pathBounds.Width == 0) ? 1 : width / pathBounds.Width;
			float scaleY = (height == 0 || pathBounds.Height == 0) ? 1 : height / pathBounds.Height;

            //TEMP_MATRIX.Reset();
            Matrix tempMatrix = Matrix.Identity;
			tempMatrix = MatrixExtensions.TranslateBy(tempMatrix, x, y);
            tempMatrix = MatrixExtensions.ScaleBy(tempMatrix, scaleX, scaleY);
            tempMatrix = MatrixExtensions.TranslateBy(tempMatrix, -pathBounds.X, -pathBounds.Y);
		
			path.Transform(TEMP_MATRIX);
		}

		/// <summary>
		/// Returns true if this path intersects the given rectangle.
		/// </summary>
		/// <remarks>
		/// This method first checks if the interior of the path intersects with the rectangle.
		/// If not, the method then checks if the path bounding the pen stroke intersects with
		/// the rectangle.  If either of these cases are true, this method returns true.
		/// <para>
		/// <b>Performance Note</b>:  For some paths, this method can be very slow.  This is due
		/// to the implementation of IsVisible.  The problem usually occurs when many lines are
		/// joined at very steep angles.  See <see cref="PPath">PPath Overview</see> for workarounds.
		/// </para>
		/// </remarks>
		/// <param name="bounds">The rectangle to check for intersection.</param>
		/// <returns>True if this path intersects the given rectangle; otherwise, false.</returns>
		public override bool Intersects(RectangleFx bounds) 
        {
			// Call intersects with the identity matrix.
			return Intersects(bounds, Matrix.Identity);
		}

		/// <summary>
		/// Overridden.  Performs picking in canvas coordinates if <see cref="PickMode">PickMode</see>
		/// is false.
		/// </summary>
		/// <remarks>
		/// Due to the implementation of the GraphicsPath object, picking in canvas coordinates
		/// is more accurate, but will introduce a significant performance hit.
		/// </remarks>
		protected override bool PickAfterChildren(PPickPath pickPath) {
			if (pickMode == PathPickMode.Fast) {
				return base.PickAfterChildren(pickPath);
			} else {
				return Intersects(pickPath.PickBounds,  pickPath.GetPathTransformTo(this));
			}
		}

		/// <summary>
		/// Returns true if this path intersects the given rectangle.
		/// </summary>
		/// <remarks>
		/// This method first checks if the interior of the path intersects with the rectangle.
		/// If not, the method then checks if the path bounding the pen stroke intersects with
		/// the rectangle.  If either of these cases are true, this method returns true.
		/// <para>
		/// <b>Performance Note</b>:  For some paths, this method can be very slow.  This is due
		/// to the implementation of IsVisible.  The problem usually occurs when many lines are
		/// joined at very steep angles.  See <see cref="PPath">PPath Overview</see> for workarounds.
		/// </para>
		/// </remarks>
		/// <param name="bounds">The rectangle to check for intersection.</param>
		/// <param name="matrix">
		/// A matrix object that specifies a transform to apply to the path and bounds before
		/// checking for an intersection.
		/// </param>
		/// <returns>True if this path intersects the given rectangle; otherwise, false.</returns>
		public virtual bool Intersects(RectangleFx bounds, Matrix matrix) 
        {
			if (base.Intersects(bounds)) 
            {
				// Transform the bounds.
				if (!(matrix == Matrix.Identity)) 
                    bounds = MatrixExtensions.Transform(matrix, bounds);

				// Set the temp region to the transformed path.
				SetTempRegion(path, matrix, false);

				if (Brush != Color.Transparent && TEMP_REGION.IntersectsWith(bounds)) 
                {
					return true;
				} 
                else if (pen != null) 
                {
					// Set the temp region to the transformed, widened path.
					SetTempRegion(path, matrix, true);
					return TEMP_REGION.IntersectsWith(bounds);
				}
			}
		
			return false;
		}

		/// <summary>
		/// Sets the temp region to the transformed path, widening the path if
		/// requested to do so.
		/// </summary>
		private void SetTempRegion(XnaGraphicsPath path, Matrix matrix, bool widen) 
        {
			TEMP_PATH.Reset();

			if (path.PointCount > 0) 
            {
				TEMP_PATH.AddPath(path, false);

				if (widen) 
                {
					TEMP_PATH.Widen(pen, matrix);
				} 
                else 
                {
					TEMP_PATH.Transform(matrix);
				}
			}

			//TEMP_REGION.MakeInfinite();
			//TEMP_REGION.Intersect(TEMP_PATH);
		}

		/// <summary>
		/// This method is called to update the bounds whenever the underlying path changes.
		/// </summary>
		public virtual void UpdateBoundsFromPath() 
        {
			updatingBoundsFromPath = true;

			if (path == null || path.PointCount == 0) 
            {
				ResetBounds();
			} 
            else 
            {
				try 
                {
					TEMP_PATH.Reset();
					TEMP_PATH.AddPath(path, false);
					if (pen != null && TEMP_PATH.PointCount > 0) TEMP_PATH.Widen(pen);
                    RectangleFx b = TEMP_PATH.GetBounds();
					SetBounds(b.X, b.Y, b.Width, b.Height);
				} 
                catch (OutOfMemoryException) 
                {
					//Catch the case where the path is a single point
				}
			}
			updatingBoundsFromPath = false;
		}
		#endregion

		#region Painting
		//****************************************************************
		// Painting - Methods for painting a PPath.
		//****************************************************************

		/// <summary>
		/// Overridden.  See <see cref="PNode.Paint">PNode.Paint</see>.
		/// </summary>
		protected override void Paint(PPaintContext paintContext) 
        {
			//System.Drawing.Brush b = this.Brush;
			XnaGraphics g = paintContext.Graphics;

			if (this.Brush != Color.Transparent) 
            {
                //PointFx transformedPosition = MatrixExtensions.Transform(this.Matrix, new PointFx(1.0f, 1.0f));
                g.FillPath(this.Matrix, this.Brush, path);
			}

			if (pen != null) 
            {
				g.DrawPath(pen, path);
			}
		}
		#endregion

		#region Path Support
		//****************************************************************
		// Path Support - Methods for manipulating the underlying path.
		// See System.Drawing.Drawing2D.GraphicsPath documentation for
		// more information on using these methods.
		//****************************************************************

		/// <summary>
		/// Occurs when there is a change in this node's <see cref="Pen">Pen</see>.
		/// </summary>
		/// <remarks>
		/// When a user attaches an event handler to the PathChanged Event as in
		/// PathChanged += new PPropertyEventHandler(aHandler),
		/// the add method adds the handler to the delegate for the event
		/// (keyed by PROPERTY_KEY_PATH in the Events list).
		/// When a user removes an event handler from the PathChanged event as in 
		/// PathChanged -= new PPropertyEventHandler(aHandler),
		/// the remove method removes the handler from the delegate for the event
		/// (keyed by PROPERTY_KEY_PATH in the Events list).
		/// </remarks>
		public virtual event PPropertyEventHandler PathChanged 
        {
			add { HandlerList.AddHandler(PROPERTY_KEY_PATH, value); }
			remove { HandlerList.RemoveHandler(PROPERTY_KEY_PATH, value); }
		}

		/// <summary>
		/// Gets a reference to the underlying path object.
		/// </summary>
		/// <value>The underlying path object.</value>
		public virtual XnaGraphicsPath PathReference 
        {
			get { return path; }
		}

		/// <summary>
		/// See <see cref="GraphicsPath.FillMode">GraphicsPath.FillMode</see>.
		/// </summary>
        //public virtual System.Drawing.Drawing2D.FillMode FillMode 
        //{
        //    get { return path.FillMode; }
        //    set 
        //    {
        //        path.FillMode = value;
        //        InvalidatePaint();
        //    }
        //}

		/// <summary>
		/// See <see cref="GraphicsPath.PathData">GraphicsPath.PathData</see>.
		/// </summary>
		public virtual PathData PathData 
        {
			get { return path.PathData; }
		}

		/// <summary>
		/// See <see cref="GraphicsPath.PointCount">GraphicsPath.PointCount</see>.
		/// </summary>
		public virtual int PointCount 
        {
			get { return path.PointCount; }
		}

		/// <summary>
		/// See <see cref="GraphicsPath.AddArc(float, float, float, float, float, float)">
		/// GraphicsPath.AddArc</see>.
		/// </summary>
		public virtual void AddArc(float x, float y, float width, float height, float startAngle, float sweepAngle) 
        {
			path.AddArc(x, y, width, height, startAngle, sweepAngle);
			FirePropertyChangedEvent(PROPERTY_KEY_PATH, PROPERTY_CODE_PATH, null, path);
			UpdateBoundsFromPath();
			InvalidatePaint();
		}

		/// <summary>
		/// See <see cref="GraphicsPath.AddBezier(float, float, float, float, float, float, float, float)">
		/// GraphicsPath.AddBezier</see>.
		/// </summary>
		public virtual void AddBezier(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4) 
        {
			path.AddBezier(x1, y1, x2, y2, x3, y3, x4, y4);
			FirePropertyChangedEvent(PROPERTY_KEY_PATH, PROPERTY_CODE_PATH, null, path);
			UpdateBoundsFromPath();
			InvalidatePaint();
		}

		/// <summary>
		/// See <see cref="GraphicsPath.AddClosedCurve(PointFx[])">GraphicsPath.AddClosedCurve</see>.
		/// </summary>
		public virtual void AddClosedCurve(PointFx[] points) 
        {
			path.AddClosedCurve(points);
			FirePropertyChangedEvent(PROPERTY_KEY_PATH, PROPERTY_CODE_PATH, null, path);
			UpdateBoundsFromPath();
			InvalidatePaint();
		}

		/// <summary>
		/// See <see cref="GraphicsPath.AddCurve(PointFx[])">GraphicsPath.AddCurve</see>.
		/// </summary>
		public virtual void AddCurve(PointFx[] points) 
        {
			path.AddCurve(points);
			FirePropertyChangedEvent(PROPERTY_KEY_PATH, PROPERTY_CODE_PATH, null, path);
			UpdateBoundsFromPath();
			InvalidatePaint();
		}

		/// <summary>
		/// See <see cref="GraphicsPath.AddEllipse(float, float, float, float)">
		/// GraphicsPath.AddEllipse</see>.
		/// </summary>
		public virtual void AddEllipse(float x, float y, float width, float height) 
        {
			path.AddEllipse(x, y, width, height);
			FirePropertyChangedEvent(PROPERTY_KEY_PATH, PROPERTY_CODE_PATH, null, path);
			UpdateBoundsFromPath();
			InvalidatePaint();
		}

		/// <summary>
		/// See <see cref="GraphicsPath.AddLine(float, float, float, float)">GraphicsPath.AddLine</see>.
		/// </summary>
		public virtual void AddLine(float x1, float y1, float x2, float y2) 
        {
			path.AddLine(x1, y1, x2, y2);
			FirePropertyChangedEvent(PROPERTY_KEY_PATH, PROPERTY_CODE_PATH, null, path);
			UpdateBoundsFromPath();
			InvalidatePaint();
		}

		/// <summary>
		/// See <see cref="GraphicsPath.AddPath(GraphicsPath, bool)">GraphicsPath.AddPath</see>.
		/// </summary>
		public virtual void AddPath(XnaGraphicsPath path, bool connect) 
        {
			this.path.AddPath(path, connect);
			FirePropertyChangedEvent(PROPERTY_KEY_PATH, PROPERTY_CODE_PATH, null, path);
			UpdateBoundsFromPath();
			InvalidatePaint();
		}

		/// <summary>
		/// See <see cref="GraphicsPath.AddPolygon(PointFx[])">GraphicsPath.AddPolygon</see>.
		/// </summary>
		public virtual void AddPolygon(PointFx[] points) 
        {
			path.AddPolygon(points);
			FirePropertyChangedEvent(PROPERTY_KEY_PATH, PROPERTY_CODE_PATH, null, path);
			UpdateBoundsFromPath();
			InvalidatePaint();
		}

		/// <summary>
		/// See <see cref="GraphicsPath.AddRectangle(RectangleFx)">
		/// GraphicsPath.AddRectangle</see>.
		/// </summary>
		public virtual void AddRectangle(float x, float y, float width, float height) 
        {
			path.AddRectangle(new RectangleFx(x, y, width, height));
			FirePropertyChangedEvent(PROPERTY_KEY_PATH, PROPERTY_CODE_PATH, null, path);
			UpdateBoundsFromPath();
			InvalidatePaint();
		}

		/// <summary>
		/// See <see cref="GraphicsPath.CloseFigure">GraphicsPath.CloseFigure</see>.
		/// </summary>
		public virtual void CloseFigure() 
        {
			path.CloseFigure();
			FirePropertyChangedEvent(PROPERTY_KEY_PATH, PROPERTY_CODE_PATH, null, path);
			UpdateBoundsFromPath();
			InvalidatePaint();
		}

		/// <summary>
		/// See <see cref="GraphicsPath.CloseAllFigures">GraphicsPath.CloseAllFigures</see>.
		/// </summary>
		public virtual void CloseAllFigures() 
        {
			path.CloseAllFigures();
			FirePropertyChangedEvent(PROPERTY_KEY_PATH, PROPERTY_CODE_PATH, null, path);
			UpdateBoundsFromPath();
			InvalidatePaint();
		}

		/// <summary>
		/// See <see cref="GraphicsPath.Reset">GraphicsPath.Reset</see>.
		/// </summary>
		public virtual void Reset() 
        {
			path.Reset();
			FirePropertyChangedEvent(PROPERTY_KEY_PATH, PROPERTY_CODE_PATH, null, path);
			UpdateBoundsFromPath();
			InvalidatePaint();
		}
		#endregion

		#region Serialization
		//****************************************************************
		// Serialization - Nodes conditionally serialize their parent.
		// This means that only the parents that were unconditionally
		// (using GetObjectData) serialized by someone else will be restored
		// when the node is deserialized.
		//****************************************************************

		/// <summary>
		/// Read this PPath and all of its descendent nodes from the given SerializationInfo.
		/// </summary>
		/// <param name="info">The SerializationInfo to read from.</param>
		/// <param name="context">The StreamingContext of this serialization operation.</param>
		/// <remarks>
		/// This constructor is required for Deserialization.
		/// </remarks>
		protected PPath(SerializationInfo info, StreamingContext context) :
			base(info, context) {

			pen = PUtil.ReadPen(info);
		}

		/// <summary>
		/// Write this PPath and all of its descendent nodes to the given SerializationInfo.
		/// </summary>
		/// <param name="info">The SerializationInfo to write to.</param>
		/// <param name="context">The streaming context of this serialization operation.</param>
		/// <remarks>
		/// This node's parent is written out conditionally, that is it will only be written out
		/// if someone else writes it out unconditionally.
		/// </remarks>
		public override void GetObjectData(SerializationInfo info, StreamingContext context) {
			base.GetObjectData (info, context);

			PUtil.WritePen(pen, info);
		}

		#endregion

		#region Debugging
		//****************************************************************
		// Debugging - Methods for debugging.
		//****************************************************************

		/// <summary>
		/// Overridden.  Gets a string representing the state of this node.
		/// </summary>
		/// <value>A string representation of this node's state.</value>
		/// <remarks>
		/// This property is intended to be used only for debugging purposes, and the content
		/// and format of the returned string may vary between implementations. The returned
		/// string may be empty but may not be <c>null</c>.
		/// </remarks>
		protected override String ParamString {
			get {
				StringBuilder result = new StringBuilder();

				result.Append("path=" + (path == null ? "null" : path.ToString()));
				result.Append(",pen=" + (pen == null ? "null" : pen.ToString()));
				if (pen != null) result.Append("[" + pen.Color + "]");
				result.Append(',');
				result.Append(base.ParamString);

				return result.ToString();
			}
		}
		#endregion
	}
}