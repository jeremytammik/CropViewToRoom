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
    public void CropAroundRoom( Autodesk.Revit.DB.Architecture.Room room, Autodesk.Revit.DB.View view ) //This provides the required shape now how to combine with the offset and tie into wall thickness
    {
      if( view != null )
      {
        IList<IList<Autodesk.Revit.DB.BoundarySegment>> segments = room.GetBoundarySegments( new SpatialElementBoundaryOptions() );

        if( null != segments )  //the room may not be bound
        {
          foreach( IList<Autodesk.Revit.DB.BoundarySegment> segmentList in segments )
          {
            CurveLoop loop = new CurveLoop();
            foreach( Autodesk.Revit.DB.BoundarySegment boundarySegment in segmentList )
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

    public void CropAroundRoomWithOffset( Autodesk.Revit.DB.Architecture.Room room, Autodesk.Revit.DB.View view )
    {
      List<XYZ> points = new List<XYZ>();
      if( view != null )
      {
        IList<IList<Autodesk.Revit.DB.BoundarySegment>> segments = room.GetBoundarySegments( new SpatialElementBoundaryOptions() );

        if( null != segments )
        {
          foreach( IList<Autodesk.Revit.DB.BoundarySegment> segmentList in segments )
          {
            CurveLoop loop = new CurveLoop();
            foreach( Autodesk.Revit.DB.BoundarySegment boundarySegment in segmentList )
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

    public Result Execute(
      ExternalCommandData commandData,
      ref string message,
      ElementSet elements )
    {
      UIApplication uiapp = commandData.Application;
      UIDocument uidoc = uiapp.ActiveUIDocument;
      Application app = uiapp.Application;
      Document doc = uidoc.Document;

      FilteredElementCollector levels
        = new FilteredElementCollector( doc )
          .OfClass( typeof( Level ) );

      using( Transaction tx = new Transaction( doc ) )
      {
        tx.Start( "Create cropped views for each room" );

        string date_iso = DateTime.Now.ToString( "yyyy-MM-dd" );

        foreach( Level level in levels )
        {
          Debug.Print( level.Name );

          ElementId id_view = level.FindAssociatedPlanViewId();
          ViewPlan view = doc.GetElement( id_view ) as ViewPlan;

          IEnumerable<Room> rooms
            = new FilteredElementCollector( doc, id_view )
              .OfClass( typeof( SpatialElement ) )
              .Where<Element>( e => e is Room )
              .Cast<Room>();

          foreach( Room room in rooms )
          {
            string view_name = string.Format( 
              "{0}_cropped_to_room_{1}_date_{2}",
              view.Name, room.Name, date_iso );

            id_view = view.Duplicate( 
              ViewDuplicateOption.AsDependent );

            View view_cropped = doc.GetElement( id_view ) 
              as View;

            view_cropped.Name = view_name;

            view_cropped.CropBoxVisible = true;
            view_cropped.CropBoxActive = true;
            CropAroundRoom( room, view_cropped );
          }
        }
        tx.Commit();
      }
      return Result.Succeeded;
    }
  }
}
