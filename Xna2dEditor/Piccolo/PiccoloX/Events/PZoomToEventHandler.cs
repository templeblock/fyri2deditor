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
using System.Windows.Forms;
using System.Drawing;

using XnaPiccolo;
using XnaPiccolo.Event;
using XnaPiccolo.Util;
using Xna2dEditor;

namespace XnaPiccoloX.Events {
	/// <summary>
	/// <b>PZoomToEventHandler</b> is used to zoom the camera view to the node
	/// clicked on with button one.
	/// </summary>
	public class PZoomToEventHandler : PBasicInputEventHandler {
		#region Constructors
		/// <summary>
		/// Constructs a new PZoomToEventHandler.
		/// </summary>
		public PZoomToEventHandler() {
			this.AcceptsEvent = new AcceptsEventDelegate(PZoomToEventHandlerAcceptsEvent);
		}
		#endregion

		#region Zoom To
		/// <summary>
		/// The filter for a PZoomToEventHandler.  This method only accepts left mouse button
		/// events that have not yet been handled.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		/// <returns>
		/// True if the event is an unhandled left mouse button event; otherwise, false.
		/// </returns>
		protected virtual bool PZoomToEventHandlerAcceptsEvent(PInputEventArgs e) {
			if (!e.Handled && e.IsMouseEvent && e.Button == MouseButtons.Left) {
				return true;
			}
			return false;
		}

		/// <summary>
		/// Overridden.  See <see cref="PBasicInputEventHandler.OnMouseDown">
		/// PBasicInputEventHandler.OnMouseDown</see>.
		/// </summary>
		public override void OnMouseDown(object sender, PInputEventArgs e) {
			ZoomTo(e);
		}

		/// <summary>
		/// Animates the camera's view, panning and scaling when necessary, to fully fit the
		/// bounds of the picked node into the camera's view bounds.
		/// </summary>
		/// <param name="e">A PInputEventArgs that contains the event data.</param>
		protected virtual void ZoomTo(PInputEventArgs e) {
			RectangleFx zoomToBounds;
			PNode picked = e.PickedNode;
		
			if (picked is PCamera) {
				PCamera c = (PCamera) picked;
				zoomToBounds = c.UnionOfLayerFullBounds;
			} else {
				zoomToBounds = picked.GlobalFullBounds;
			}
		
			e.Camera.AnimateViewToCenterBounds(zoomToBounds, true, 500);
		}
		#endregion
	}
}