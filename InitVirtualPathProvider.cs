using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Hosting;

namespace idseefeld.de.UmbracoAzure
{
  /// <summary> 
  ///   Contains the application initialization method 
  ///   for the sample application. 
  /// </summary> 
  public static class AppStart
  {
    public static void AppInitialize()
    {
      SamplePathProvider sampleProvider = new SamplePathProvider();
      HostingEnvironment.RegisterVirtualPathProvider(sampleProvider);
    } 
  }
}