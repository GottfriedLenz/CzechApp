using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using Microsoft.Extensions.Configuration;

namespace WebApiBasic.Controllers
{
    
    public class ReportController : ApiController
    {
        
        public IHttpActionResult Get()
        {
            var MyConfig = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            var cstr = MyConfig.GetSection("ConnectionStrings")["DefaultConnection"];
            var ccodes = MyConfig.GetSection("AppSettings")["CurrencyCodes"];


            var queryWithForJson = "SELECT Code,  cast(max(Rate / Amount) as numeric(15, 3)) as MaxValue, cast(avg(Rate / Amount) as numeric(15, 3)) as AvgValue, cast(min(Rate / Amount) as numeric (15, 3)) as MinValue FROM dbo.ExchangeRatesHistory WHERE Code in ("+ ccodes + ")group by Code FOR JSON AUTO";
            var conn = new SqlConnection(cstr);
            var cmd = new SqlCommand(queryWithForJson, conn);
            conn.Open();
            var jsonResult = new StringBuilder();
            var reader = cmd.ExecuteReader();
            if (!reader.HasRows)
            {
                jsonResult.Append("[]");
            }
            else
            {
                while (reader.Read())
                {
                    jsonResult.Append(reader.GetValue(0).ToString());
                }
            }

         
            /// get JSON
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            response.Content = new StringContent(jsonResult.ToString());
            return ResponseMessage(response);
        }
    }
}
