using JIRAToModayAPI.Models;

namespace JIRAToModayAPI.DTO
{
    public static class MondayItemMapper
    {
        private static string ExtractDescriptionText(JiraDescription? description)
        {
            if (description?.Content == null)
                return string.Empty;

            var paragraphs = description.Content
                .Where(c => c.Type == "paragraph")
                .Select(c => string.Join("", c.Content?
                    .Where(t => t.Type == "text")
                    .Select(t => t.Text) ?? new string[0]))
                .Where(p => !string.IsNullOrWhiteSpace(p));

            return string.Join("\n\n", paragraphs);
        }
        public static CreateItemRequest ToCreateItemRequest(JiraIssue issue)
        {
            return new CreateItemRequest
            {
                boardId = 6246909093, // Replace with actual board ID
                groupId = "emailed_items__1", // Replace with actual group ID
                issueKey = issue.Key,
                issueId = issue.Id,
                itemName = issue.Fields.Summary,
                columnValues = new Dictionary<string, string>
                {
                    { "long_text", ExtractDescriptionText(issue.Fields.Description) },
                    { "text_mktma7yj", issue.Id },
                    { "text_mktm4y2m", issue.Key },
                    { "status_11", "VAR Input" }
                },
                attachments = issue.Fields.Attachments?.Select(a => new JiraAttachment
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
                }).ToList() ?? new List<JiraAttachment>()
            };
        }

        public static List<CreateItemRequest> ToCreateItemRequest(JiraSearchResponse jiraResponse)
        {

            List<CreateItemRequest> createItemRequests = new List<CreateItemRequest>();



            foreach (var issue in jiraResponse.Issues)
            {
                createItemRequests.Add(ToCreateItemRequest(issue));
            }
            
            return createItemRequests;

        }
        public static UpdateItemRequest ToUpdateItemRequest(JiraIssue issue,string itemId)
        {

            return new UpdateItemRequest
            {
                itemId =itemId, // Assuming itemId is the same as issue Id
                boardId = 6246909093, // Replace with actual board ID
                columnValues = new Dictionary<string, string>
                {
                    { "long_text", ExtractDescriptionText(issue.Fields.Description) },
                    { "text_mktma7yj", issue.Id },
                    { "text_mktm4y2m", issue.Key },
                    { "status_11", "VAR Input" }
                },
                attachments = issue.Fields.Attachments?.Select(a => new JiraAttachment
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
                }).ToList() ?? new List<JiraAttachment>()
            };
         
        }
    }
}
