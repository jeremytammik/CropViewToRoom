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


          }
        }
        tx.Commit();
      }
      return Result.Succeeded;
    }
  }
}
