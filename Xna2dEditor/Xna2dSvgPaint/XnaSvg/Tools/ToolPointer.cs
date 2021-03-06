#region Header

/*  --------------------------------------------------------------------------------------------------------------
 *  I Software Innovations
 *  --------------------------------------------------------------------------------------------------------------
 *  SVG Artieste 2.0
 *  --------------------------------------------------------------------------------------------------------------
 *  File     :       ToolPointer.cs
 *  Author   :       ajaysbritto@yahoo.com
 *  Date     :       20/03/2010
 *  --------------------------------------------------------------------------------------------------------------
 *  Change Log
 *  --------------------------------------------------------------------------------------------------------------
 *  Author	Comments
 */

#endregion Header

namespace Fyri2dEditor
{
    using System.Collections;
    using System.Windows.Forms;

    using Draw;
    using Microsoft.Xna.Framework;

    /// <summary>
    /// Pointer tool
    /// </summary>
    public class ToolPointer : Tool
    {
        #region Fields

        // Keep state about last and current point (used to move and resize objects)
        private Point _lastPoint = new Point(0,0);

        // Object which is currently resized:
        private XnaDrawObject _resizedObject;
        private int _resizedObjectHandle;
        private SelectionMode _selectMode = SelectionMode.None;
        private Point _startPoint = new Point(0, 0);

        #endregion Fields

        #region Constructors

        public ToolPointer()
        {
            IsComplete = true;
        }

        #endregion Constructors

        #region Enumerations

        private enum SelectionMode
        {
            None,
            NetSelection,   // group selection is active
            Move,           // object(s) are moves
            Size            // object is resized
        }

        #endregion Enumerations

        #region Methods

        /// <summary>
        /// Left mouse button is pressed
        /// </summary>
        /// <param name="drawArea"></param>
        /// <param name="e"></param>
        public override void OnMouseDown(Xna2dDrawArea drawArea, MouseEventArgs e)
        {
            _selectMode = SelectionMode.None;
            Point point = new Point(drawArea.MyMouseX, drawArea.MyMouseY);

            // Test for resizing (only if control is selected, cursor is on the handle)
            int n = drawArea.GraphicsList.SelectionCount;

                for ( int i = 0; i < n; i++ )
                {
                    XnaDrawObject o = drawArea.GraphicsList.GetSelectedObject(i);
                    int handleNumber = o.HitTest(point);
                    bool hitOnOutline = o.HitOnCircumferance;

                    if ( handleNumber > 0 )
                    {
                        _selectMode = SelectionMode.Size;

                        // keep resized object in class members
                        _resizedObject = o;
                        _resizedObjectHandle = handleNumber;

                        // Since we want to resize only one object, unselect all other objects
                        drawArea.GraphicsList.UnselectAll();
                        o.Selected = true;
                        o.MouseClickOnHandle(handleNumber);

                        break;
                    }

                    if (hitOnOutline && (n == 1)) //only one item is selected
                    {
                        _selectMode = SelectionMode.Size;
                        o.MouseClickOnBorder();
                        o.Selected = true;
                    }

            }

            // Test for move (cursor is on the object)
            if ( _selectMode == SelectionMode.None )
            {
                int n1 = drawArea.GraphicsList.Count;
                XnaDrawObject o = null;

                for ( int i = 0; i < n1; i++ )
                {
                    if ( drawArea.GraphicsList[i].HitTest(point) == 0 )
                    {
                        o = drawArea.GraphicsList[i];
                        break;
                    }
                }

                if ( o != null )
                {
                    _selectMode = SelectionMode.Move;

                    // Unselect all if Ctrl is not pressed and clicked object is not selected yet
                    if ( ( Control.ModifierKeys & Keys.Control ) == 0  && !o.Selected )
                        drawArea.GraphicsList.UnselectAll();

                    // Select clicked object
                    o.Selected = true;

                    drawArea.Cursor = Cursors.SizeAll;
                }
            }

            // Net selection
            if ( _selectMode == SelectionMode.None )
            {
                // click on background
                if ( ( Control.ModifierKeys & Keys.Control ) == 0 )
                    drawArea.GraphicsList.UnselectAll();

                _selectMode = SelectionMode.NetSelection;
                drawArea.DrawNetRectangle = true;
            }

            _lastPoint.X = drawArea.MyMouseX;
            _lastPoint.Y = drawArea.MyMouseY;
            _startPoint.X = drawArea.MyMouseX;
            _startPoint.Y = drawArea.MyMouseY;

            drawArea.Capture = true;
            drawArea.NetRectangle = XnaDrawRectangle.GetNormalizedRectangle(_startPoint, _lastPoint);

            drawArea.Refresh();
        }

        /// <summary>
        /// Mouse is moved.
        /// None button is pressed, ot left button is pressed.
        /// </summary>
        /// <param name="drawArea"></param>
        /// <param name="e"></param>
        public override void OnMouseMove(Xna2dDrawArea drawArea, MouseEventArgs e)
        {
            var point = new Point(drawArea.MyMouseX, drawArea.MyMouseY);

            // set cursor when mouse button is not pressed
            if ( e.Button == MouseButtons.None )
            {
                Cursor cursor = null;

                for ( int i = 0; i < drawArea.GraphicsList.Count; i++ )
                {
                    int n = drawArea.GraphicsList[i].HitTest(point);

                    if ( n > 0 )
                    {
                        cursor = drawArea.GraphicsList[i].GetHandleCursor(n);
                        break;
                    }
                    if (drawArea.GraphicsList[i].HitOnCircumferance)
                    {
                        cursor = drawArea.GraphicsList[i].GetOutlineCursor(n);
                        break;
                    }
                }

                if ( cursor == null )
                    cursor = Cursors.Default;

                drawArea.Cursor = cursor;

                return;
            }

            if ( e.Button != MouseButtons.Left )
                return;

            // Left button is pressed

            // Find difference between previous and current position
            float dx = drawArea.MyMouseX - _lastPoint.X;
            float dy = drawArea.MyMouseY - _lastPoint.Y;

            _lastPoint.X = drawArea.MyMouseX;
            _lastPoint.Y = drawArea.MyMouseY;

            // resize
            if ( _selectMode == SelectionMode.Size )
            {
                if ( _resizedObject != null )
                {
                    _resizedObject.MoveHandleTo(point, _resizedObjectHandle);
                    drawArea.SetDirty();
                    drawArea.Refresh();
                }
            }

            // move
            if ( _selectMode == SelectionMode.Move )
            {
                int n = drawArea.GraphicsList.SelectionCount;

                for ( int i = 0; i < n; i++ )
                {
                    drawArea.GraphicsList.GetSelectedObject(i).Move(dx, dy);
                }

                drawArea.Cursor = Cursors.SizeAll;
                drawArea.SetDirty();
                drawArea.Refresh();
            }

            if ( _selectMode == SelectionMode.NetSelection )
            {
                drawArea.NetRectangle = XnaDrawRectangle.GetNormalizedRectangle(_startPoint, _lastPoint);
                drawArea.Refresh();
                return;
            }
        }

        /// <summary>
        /// Right mouse button is released
        /// </summary>
        /// <param name="drawArea"></param>
        /// <param name="e"></param>
        public override void OnMouseUp(Xna2dDrawArea drawArea, MouseEventArgs e)
        {
            if ( _selectMode == SelectionMode.NetSelection )
            {
                // Group selection
                drawArea.GraphicsList.SelectInRectangle(drawArea.NetRectangle);

                _selectMode = SelectionMode.None;
                drawArea.DrawNetRectangle = false;
            }

            if ( _resizedObject != null )
            {
                // after resizing
                _resizedObject.Normalize();
                _resizedObject = null;
                drawArea.ResizeCommand(drawArea.GraphicsList.GetFirstSelected(),
                    new Point(_startPoint.X, _startPoint.Y),
                    new Point(drawArea.MyMouseX, drawArea.MyMouseY),
                    _resizedObjectHandle);
            }

            drawArea.Capture = false;
            drawArea.Refresh();

            //push the command to undo/Redo list now
            if (_selectMode == SelectionMode.Move)
            {
                var movedItemsList = new ArrayList();

                for (int i = 0; i < drawArea.GraphicsList.SelectionCount; i++)
                {
                    movedItemsList.Add(drawArea.GraphicsList.GetSelectedObject(i));
                }

                var delta = new Point {X = drawArea.MyMouseX - _startPoint.X, Y = drawArea.MyMouseY - _startPoint.Y};
                drawArea.MoveCommand(movedItemsList, delta);
            }

            IsComplete = true;
        }

        #endregion Methods
    }
}