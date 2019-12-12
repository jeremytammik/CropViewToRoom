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
    public Result Execute(
      ExternalCommandData commandData,
      ref string message,
      ElementSet elements )
    {
      UIApplication uiapp = commandData.Application;
      UIDocument uidoc = uiapp.ActiveUIDocument;
      Application app = uiapp.Application;
      Document doc = uidoc.Document;
      IList<double> wallthicknessList = new List<double>();
      XYZ normal = XYZ.BasisZ;

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
          wallthicknessList.Clear();
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
            wallthicknessList.Clear();
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

            CurveLoop loop = null;
            foreach( IList<BoundarySegment> sloop in sloops )
            {
              loop = new CurveLoop();
              foreach( BoundarySegment s in sloop )
              {
                loop.Append( s.GetCurve() );
                ElementType type = doc.GetElement( 
                  s.ElementId ) as ElementType;

                Element elem = doc.GetElement( s.ElementId );
                if( elem is Wall )
                {
                  Wall wall = elem as Wall;
                  wallthicknessList.Add( wall.Width + 0.1 );
                }
                else
                {
                  //Room separator
                  //Any other exceptions to walls need including??
                  wallthicknessList.Add( 0.1 );
                }
              }
              // Skip out after first sloop - ignore
              // rooms with holes and disjunct parts
              break;
            }

            CurveLoop loop2 = CurveLoop.CreateViaOffset(
              loop, wallthicknessList, normal );

            CurveLoop newloop = new CurveLoop();

            foreach( Curve curve in loop2 )
            {
              IList<XYZ> points = curve.Tessellate();

              for( int ip = 0; ip < points.Count - 1; ip++ )
              {
                Line l = Line.CreateBound( 
                  points[ ip ], points[ ip + 1 ] );

                newloop.Append( l );
              }
            }
            ViewCropRegionShapeManager vcrs_mgr 
              = view_cropped.GetCropRegionShapeManager();

            bool valid = vcrs_mgr.IsCropRegionShapeValid( 
              newloop );

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
  }
}
