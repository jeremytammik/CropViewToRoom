#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
#endregion

namespace CropViewToRoom
{
  [Transaction( TransactionMode.Manual )]
    public class Command : IExternalCommand
    {
    #region Code by Stephen Harrison
    public void CropAroundRoom( Room room, View view ) //This provides the required shape now how to combine with the offset and tie into wall thickness
    {
      if( view != null )
      {
        IList<IList<BoundarySegment>> segments = room.GetBoundarySegments( new SpatialElementBoundaryOptions() );

        if( null != segments )  //the room may not be bound
        {
          foreach( IList<BoundarySegment> segmentList in segments )
          {
            CurveLoop loop = new CurveLoop();
            foreach( BoundarySegment boundarySegment in segmentList )
            {
              List<XYZ> points = boundarySegment.GetCurve().Tessellate().ToList();
              for( int ip = 0; ip < points.Count - 1; ip++ )
              {
                Line l = Line.CreateBound( points[ ip ], points[ ip + 1 ] );
                loop.Append( l );
              }
            }
            ViewCropRegionShapeManager vcrShapeMgr = view.GetCropRegionShapeManager();
            bool cropValid = vcrShapeMgr.IsCropRegionShapeValid( loop );
            if( cropValid )
            {
              vcrShapeMgr.SetCropShape( loop );
              break;  // if more than one set of boundary segments for room, crop around the first one
            }
          }
        }
      }
    }

    public void CropAroundRoomWithOffset( Room room, View view )
    {
      List<XYZ> points = new List<XYZ>();
      if( view != null )
      {
        IList<IList<BoundarySegment>> segments = room.GetBoundarySegments( new SpatialElementBoundaryOptions() );

        if( null != segments )
        {
          foreach( IList<BoundarySegment> segmentList in segments )
          {
            CurveLoop loop = new CurveLoop();
            foreach( BoundarySegment boundarySegment in segmentList )
            {
              points.AddRange( boundarySegment.GetCurve().Tessellate() );
            }
            CurveLoop loop2 = new CurveLoop();
            double offset = 2.0;
            XYZ normal = new XYZ( 0, 0, -1 );
            List<XYZ> pts = OffsetPoints( points, offset, normal ).ToList();
            for( int ip = 0; ip < points.Count - 1; ip++ )
            {
              Line l = Line.CreateBound( pts[ ip ], pts[ ip + 1 ] );

              loop2.Append( l );
            }
            ViewCropRegionShapeManager vcrShapeMgr = view.GetCropRegionShapeManager();
            bool cropValid = vcrShapeMgr.IsCropRegionShapeValid( loop );
            if( cropValid )
            {
              vcrShapeMgr.SetCropShape( loop );
              break;
            }
          }
        }
      }
    }

    public static CurveLoop CreateCurveLoop( List<XYZ> pts )
    {
      int n = pts.Count;
      CurveLoop curveLoop = new CurveLoop();
      for( int i = 1; i < n; ++i )
      {
        curveLoop.Append( Line.CreateBound( pts[ i - 1 ], pts[ i ] ) );
      }
      curveLoop.Append( Line.CreateBound( pts[ n ], pts[ 0 ] ) );
      return curveLoop;
    }

    public static IEnumerable<XYZ> OffsetPoints( List<XYZ> pts, double offset, XYZ normal )
    {
      CurveLoop curveLoop = CreateCurveLoop( pts );
      CurveLoop curveLoop2 = CurveLoop.CreateViaOffset( curveLoop, offset, normal );
      return curveLoop2.Select<Curve, XYZ>( c => c.GetEndPoint( 0 ) );
    }
    #endregion // Code by Stephen Harrison

        private bool firstPass = true;
        public Result Execute(ExternalCommandData commandData, ref string message,ElementSet elements )
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;
            IList<double> wallthicknessList = new List<double>();
            XYZ normal = new XYZ(0,0,1);
            string previous = null;
			double previousWidth = 0;
            SpatialElementBoundaryOptions seb_opt = new SpatialElementBoundaryOptions();
            FilteredElementCollector levels = new FilteredElementCollector( doc ).OfClass( typeof( Level ) );
            using( Transaction tx = new Transaction( doc ) )
            {
                tx.Start( "Create cropped views for each room" );
                string date_iso = DateTime.Now.ToString( "yyyy-MM-dd" );
                foreach( Level level in levels )
                {
                    Debug.Print( level.Name );
                    ElementId id_view = level.FindAssociatedPlanViewId();
                    ViewPlan view = doc.GetElement( id_view ) as ViewPlan;
                    IEnumerable<Room> rooms = new FilteredElementCollector( doc, id_view ).OfClass( typeof( SpatialElement ) ).Where<Element>( e => e is Room ).Cast<Room>();
                    foreach( Room room in rooms )
                    {
                        wallthicknessList.Clear();
                        string view_name = string.Format("{0}_cropped_to_room_{1}_date_{2}",view.Name, room.Name, date_iso );
                        id_view = view.Duplicate(ViewDuplicateOption.AsDependent );
                        View view_cropped = doc.GetElement( id_view ) as View;
                        view_cropped.Name = view_name;
                        IList<IList<BoundarySegment>> sloops = room.GetBoundarySegments( seb_opt );
                        if( null == sloops ) // the room may not be bound
                        {
                            continue;
                        }
                        CurveLoop loop = null;
                        foreach( IList<BoundarySegment> sloop in sloops )
                        {
                            loop = new CurveLoop();
                            foreach( BoundarySegment s in sloop )
                            {
                                loop.Append( s.GetCurve() );
                                ElementType type = doc.GetElement(s.ElementId) as ElementType;
			                    Element elem = doc.GetElement(s.ElementId);
                                //Error when wall width varies across length
                                //if (elem is Wall)
                                //{
                                //    Wall wall = elem as Wall;
                                //    wallthicknessList.Add(wall.Width*1.1);
                                //}
                                //else
                                //{
                                //    //Room separator
                                //    //Any other exceptions to walls need including??
                                //    wallthicknessList.Add(0);
                                //}
                                if (elem !=null)//The elem == null is due to room separators. Are there going to be any others?
                                { 
                                    if (firstPass)
                                    {
                                        if   ((BuiltInCategory)elem.Category.Id.IntegerValue == BuiltInCategory.OST_Walls)
                                        {
                                            Wall wall = elem as Wall;
                                            //Is there a better way of identifying Orientation (predominantly horizontal or Vertical) What if walls at 45 deg?
                                            if ( Math.Abs(Math.Round(wall.Orientation.Y,0)) == 1)
                                            {
                                                previous = "Y";
                                            }
                                            else
                                            {
                                                previous = "X";
                                            }
                                            firstPass=false;
                                            wallthicknessList.Add(wall.Width+0.1);
                                            previousWidth = wall.Width+0.1;
                                        }
                                        //Are there any other situations need taking account off?
                                        else if ((BuiltInCategory)elem.Category.Id.IntegerValue == BuiltInCategory.OST_RoomSeparationLines)
                                        {
                                            //Room separator
                                            Autodesk.Revit.DB.Options opt = new Options();
                                            Autodesk.Revit.DB.GeometryElement geomElem = elem.get_Geometry(opt);
                                            foreach (GeometryObject geomObj in geomElem)
                                            {
                                                Line line = geomObj as Line;
                                                //Is there a better way of identifying Orientation (predominantly horizontal or Vertical) What if walls at 45 deg?
                                                if (0-line.GetEndPoint(1).X - line.GetEndPoint(0).X > 0-line.GetEndPoint(1).Y - line.GetEndPoint(0).Y) 
                                                {
                                                    previous = "Y";
                                                }
                                                else
                                                {
                                                    previous = "X";
                                                }
                                                firstPass=false;
                                                wallthicknessList.Add(0.1);
                                                previousWidth = 0.1;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if  ((BuiltInCategory)elem.Category.Id.IntegerValue == BuiltInCategory.OST_Walls)
                                        {
                                             Wall wall = elem as Wall;
                                            if ( Math.Abs(Math.Round(wall.Orientation.Y,0)) == 1 && previous == "Y" )
                                            {
                                                if (wall.Width > previousWidth)
                                                {
                                                    //This would appear to avoid the Cannot properly trim error
                                                    //How to allow for more than two wall thickness changes within the same length?
                                                    //How to not require this and just offset wall widths?
                                                    wallthicknessList[wallthicknessList.Count -1] = wall.Width+0.1;
                                                }
                                                wallthicknessList.Add(wall.Width+0.1);
                                                previousWidth = wall.Width+0.1;
                                                previous = "Y";
									
                                            }
                                            else if ( Math.Abs(Math.Round(wall.Orientation.X,0)) == 1 && previous == "X" )
                                            { 
                                                if (wall.Width > previousWidth)
                                                {
                                                    //This would appear to avoid the Cannot properly trim error
                                                    //How to allow for more than two wall thickness changes within the same length?
                                                    //How to not require this and just offset wall widths?
                                                    wallthicknessList[wallthicknessList.Count -1] = wall.Width+0.1;
                                                }
                                                wallthicknessList.Add(wall.Width+0.1);
                                                previousWidth = wall.Width+0.1;
                                                previous = "X";
										
                                            }
                                            else
                                            {
                                                if ( Math.Abs(Math.Round(wall.Orientation.Y,0)) == 1)
                                                {
                                                    previous = "Y";
                                                }
                                                else if ( Math.Abs(Math.Round(wall.Orientation.X,0)) == 1)
                                                {
                                                    previous = "X";
                                                }
                                                wallthicknessList.Add(wall.Width+0.1);
                                                previousWidth = wall.Width+0.1;
                                            }
                                        }
                                        else if ((BuiltInCategory)elem.Category.Id.IntegerValue == BuiltInCategory.OST_RoomSeparationLines)//Any other situations need taking account of?
                                        {
                                            //Room separator
                                            Autodesk.Revit.DB.Options opt = new Options();
                                            Autodesk.Revit.DB.GeometryElement geomElem = elem.get_Geometry(opt);
                                            foreach (GeometryObject geomObj in geomElem)
                                            {
                                                Line line = geomObj as Line;
                                                //Is there a better way of identifying Orientation (predominantly horizontal or Vertical) What if walls at 45 deg?
                                                if (0-line.GetEndPoint(1).X - line.GetEndPoint(0).X > 0-line.GetEndPoint(1).Y - line.GetEndPoint(0).Y) 
                                                {
                                                    previous = "Y";
                                                }
                                                else
                                                {
                                                    previous = "X";
                                                }
                                                wallthicknessList.Add(previousWidth);
                                                //Error "cannot trim"
                                                //wallthicknessList.Add(0.1);
                                                previousWidth = 0.1;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    wallthicknessList.Add(-0.1);
                                }
                            }
                            // Skip out after first sloop - ignore
                            // rooms with holes and disjunct parts
                            break;
                        }
                        
					    CurveLoop loop2 = CurveLoop.CreateViaOffset(loop, wallthicknessList, normal );
                        CurveLoop newloop = new CurveLoop();
                        foreach (Curve curve in loop2)
                        {
                            List<XYZ> points = curve.Tessellate().ToList();
                            for(int ip=0; ip< points.Count-1; ip++)
                            {
                                Line l = Line.CreateBound(points[ip],points[ip+1]);
                                    newloop.Append(l);
                            }
                        }
                        ViewCropRegionShapeManager vcrs_mgr = view_cropped.GetCropRegionShapeManager();
                        bool valid = vcrs_mgr.IsCropRegionShapeValid( newloop );
                        if( valid )
                        {
                            view_cropped.CropBoxVisible = true;
                            view_cropped.CropBoxActive = true;
                            vcrs_mgr.SetCropShape( newloop );
                        }
                    }
                }
                tx.Commit();
            }
            return Result.Succeeded;
        }

      //private double GetWallWidth(BoundarySegment boundarySegment) //(Autodesk.Revit.DB.Architecture.Room room)
      //  {
      //      double wallWidth = 0;
      //      ElementType type = doc.GetElement(boundarySegment.ElementId) as ElementType;
      //      Element elem = doc.GetElement(boundarySegment.ElementId);
      //      if (elem is Wall)
      //      {
      //          Wall wall = elem as Wall;
      //          wallWidth = wall.Width+0.5;
      //      }
      //      return wallWidth;
      //  }
    }
}
