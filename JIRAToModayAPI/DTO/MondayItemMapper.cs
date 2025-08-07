using JIRAToModayAPI.Models;

namespace JIRAToModayAPI.DTO
{
    public static class MondayItemMapper
    {
        public static List<CreateItemRequest> ToCreateItemRequest(JiraSearchResponse jiraResponse)
        {

            List<CreateItemRequest> createItemRequests = new List<CreateItemRequest>();



            foreach (var issue in jiraResponse.Issues)
            {
                
                CreateItemRequest request= new CreateItemRequest
                {
                    boardId = 9768483898, // Replace with actual board ID
                    groupId = "emailed_items__1", // Replace with actual group ID
                    
                    itemName = issue.Fields.Summary,
                    columnValues = new Dictionary<string, string>
                    {
                      
                        { "long_text_mktky72c", issue.Fields.Description ?? string.Empty }
                    },
                    attachments = issue.Fields.Attachments.Select(a => new JiraAttachment
                    {
                        Id = a.Id,
                        Filename = a.Filename,
                        Size = a.Size,
                        MimeType = a.MimeType,
                        Content = a.Content,
                        Created = a.Created,
                        Author = new JiraUser
                        {
                            AccountId = a.Author.AccountId,
                            DisplayName = a.Author.DisplayName,
                            EmailAddress = a.Author.EmailAddress
                        }
                    }).ToList() // Convert attachments to JiraAttachment type
                                //map Jiraresponse.issues.attachments to CreateItemRequest.Attachments




                };


                
              
                createItemRequests.Add(request);
            }
            
            return createItemRequests;

        }

    }
}
