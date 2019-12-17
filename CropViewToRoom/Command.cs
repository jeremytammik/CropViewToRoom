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
    /// <summary>
    /// Offset view crop box by an additional x feet.
    /// </summary>
    const double _additional_offset = 0.1;

    /// <summary>
    /// Multiply wall width by specific factor.
    /// </summary>
    static double _wall_width_factor = 0.1;

    void CreateModelCurves(
      View view,
      CurveLoop loop )
    {
      Document doc = view.Document;
      SketchPlane sp = view.SketchPlane;

      foreach( Curve curve in loop )
      {
        doc.Create.NewModelCurve( curve, sp );
      }
    }

    CurveLoop GetOuterLoopOfRoomFromCreateViaOffset( 
      View view,
      IList<IList<BoundarySegment>> sloops )
    {
      Document doc = view.Document;

      CurveLoop loop = null;
      IList<double> wallthicknessList = new List<double>();

      foreach( IList<BoundarySegment> sloop in sloops )
      {
        loop = new CurveLoop();

        foreach( BoundarySegment s in sloop )
        {
          loop.Append( s.GetCurve() );

          ElementType type = doc.GetElement(
            s.ElementId ) as ElementType;

          Element e = doc.GetElement( s.ElementId );

          double thickness = (e is Wall)
            ? (e as Wall).Width
            : 0; // Room separator; any other exceptions need including??

          wallthicknessList.Add( thickness
            * _wall_width_factor );
        }
        // Skip out after first sloop - ignore
        // rooms with holes and disjunct parts
        break;
      }

      int n = loop.Count();
      string slength = string.Join( ",",
        loop.Select<Curve, string>(
          c => c.Length.ToString( "#.##" ) ) );

      int m = wallthicknessList.Count();
      string sthickness = string.Join( ",",
        wallthicknessList.Select<double, string>(
          d => d.ToString( "#.##" ) ) );

      Debug.Print(
        "{0} curves with lengths {1} and {2} thicknesses {3}",
        n, slength, m, sthickness );

      CreateModelCurves( view, loop );

      bool flip_normal = true;

      XYZ normal = flip_normal ? -XYZ.BasisZ : XYZ.BasisZ;

      CurveLoop room_outer_loop = CurveLoop.CreateViaOffset(
        loop, wallthicknessList, normal );

      CreateModelCurves( view, room_outer_loop );

      //CurveLoop newloop = new CurveLoop();

      //foreach( Curve curve in loop2 )
      //{

      //  IList<XYZ> points = curve.Tessellate();

      //  for( int ip = 0; ip < points.Count - 1; ip++ )
      //  {
      //    Line l = Line.CreateBound( 
      //      points[ ip ], points[ ip + 1 ] );

      //    newloop.Append( l );
      //  }
      //}

      return room_outer_loop;
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

      SpatialElementBoundaryOptions seb_opt
        = new SpatialElementBoundaryOptions();

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

            View view_cropped = doc.GetElement(
              id_view ) as View;

            view_cropped.Name = view_name;

            IList<IList<BoundarySegment>> sloops
              = room.GetBoundarySegments( seb_opt );

            if( null == sloops ) // the room may not be bounded
            {
              continue;
            }

            CurveLoop room_outer_loop
              = GetOuterLoopOfRoomFromCreateViaOffset( 
                view_cropped, sloops );

            //ViewCropRegionShapeManager vcrs_mgr 
            //  = view_cropped.GetCropRegionShapeManager();

            //bool valid = vcrs_mgr.IsCropRegionShapeValid( 
            //  room_outer_loop );

            //if( valid )
            //{
            //  view_cropped.CropBoxVisible = true;
            //  view_cropped.CropBoxActive = true;
            //  vcrs_mgr.SetCropShape( room_outer_loop );
            //}
          }
        }
        tx.Commit();
      }
      return Result.Succeeded;
    }
  }
}