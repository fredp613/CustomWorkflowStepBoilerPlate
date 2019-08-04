using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk.Workflow;
using Microsoft.Xrm.Sdk;
using System.Net;
using System.Runtime.Serialization;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Net.Http;
using Microsoft.Xrm.Sdk.Query;

namespace Bluedrop
{
    public class Send : CodeActivity
    {

        //example input argument
        [RequiredArgument]
        [Input("Body")]
        public InArgument<string> Body { get; set; }
        //example output argment
        [Output("Response from Test API")]
        public OutArgument<string> Response { get; set; }


        protected override void Execute(CodeActivityContext executionContext)
        {
∏
            ITracingService tracingService = executionContext.GetExtension<ITracingService>();

            tracingService.Trace("Invoking Post API request");

            IWorkflowContext context = executionContext.GetExtension<IWorkflowContext>();

            Guid recordId = context.PrimaryEntityId;
            string EntityName = context.PrimaryEntityName;
            IOrganizationServiceFactory serviceFactory = executionContext.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            Entity currentRecord = service.Retrieve(EntityName, recordId, new ColumnSet(true));
            try
            {
                PlaceholderRecord myJson = new PlaceholderRecord
                {
                    UserID = currentRecord.Id,
                    Title = currentRecord.GetAttributeValue<string>("new_name"),
                    Body = this.Body.Get<string>(executionContext) //example of reading the input argument                    
                };

                using (var client = new HttpClient())
                {
                   /** MOST PRODUCTION API REQUIRE AUTHENTICATION - THIS IS NORMALLY PASSED VIA THE HTTP REQUEST HEADER, SEE EXAMPLE BELOW - NOTE 
                   THAT THE PLAHOLDER JSON SERVICE DOES NOT REQUIRE THIS */
                   // client.DefaultRequestHeaders.Add("Authorization", "bln type=api,version=1,entity=network,key=myskillspass.pro-34584,token=xxxxxxx,secret=xxxxxx");

                    var myjson = SerializerWrapper.Serialize<PlaceholderRecord>(myJson);

                    var response = client.PostAsync(
                       "https://jsonplaceholder.typicode.com/posts",
                            new StringContent(myjson, Encoding.UTF8, "application/json"));

                    string result = response.Result.ReasonPhrase;

                    if (result == "No Content")
                    {
                        this.Response.Set(executionContext, result.ToString()); //example setting the output (this value can be used in the workflow to implement additional logic)
                        Entity transaction = new Entity("new_transaction");
                        transaction["new_name"] = DateTime.Now.ToString();                      
                        //in the transaction entity, you can create a lookup field to store the record ID in context. You would add a lookup for each entity that would 
                        //leverate this workflow step or make API calls.
                        transaction["new_placeholderrecord"] = new EntityReference("new_placeholderrecord", recordId);
                        transaction["new_response"] = "Successfully sent to the Placeholder API";
                        service.Create(transaction);
                    }
                    else
                    {
                        using (HttpContent content = response.Result.Content)
                        {
                            // ... Read the string.
                            Task<string> result1 = content.ReadAsStringAsync();
                            this.Response.Set(executionContext, result.ToString());
                            Entity transaction = new Entity("new_transaction");

                            transaction["new_name"] = DateTime.Now.ToString();
                            transaction["new_placeholderrecord"] = new EntityReference("new_placeholderrecord", recordId);
                            transaction["new_response"] = result1.Result.ToString().Length > 350 ? result1.Result.ToString().Substring(0,350) : result1.Result.ToString();
                            transaction["new_longresponse"] = result1.Result.ToString();
                            service.Create(transaction);
                        
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw;               
            }
        }

    }

    [DataContract]
    public class PlaceholderRecord
    {

        [DataMember(Name = "userId")]
        public Guid UserID { get; set; }
        [DataMember(Name = "title")]
        public string Title { get; set; }
        [DataMember(Name = "body")]
        public string Body { get; set; }
        
    }

   
    internal class SerializerWrapper
    {
        public static string Serialize<T>(T srcObject)
        {
            using (MemoryStream SerializeMemoryStream = new MemoryStream())
            {
                //initialize DataContractJsonSerializer object and pass AssessmentRequestStandAloneDTO class type to it
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));

                //write newly created object(assessmentRequest) into memory stream
                serializer.WriteObject(SerializeMemoryStream, srcObject);
                string jsonString = Encoding.Default.GetString(SerializeMemoryStream.ToArray());
                return jsonString;
            }
        }

        public static T Deserialize<T>(string jsonObject)
        {
            using (MemoryStream DeSerializeMemoryStream = new MemoryStream())
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));

                StreamWriter writer = new StreamWriter(DeSerializeMemoryStream);
                writer.Write(jsonObject);
                writer.Flush();
                DeSerializeMemoryStream.Position = 0;

                T deserializedObject = (T)serializer.ReadObject(DeSerializeMemoryStream);
                return deserializedObject;
            }
        }

    }

}


