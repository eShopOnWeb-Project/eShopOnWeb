namespace Microsoft.eShopWeb.ApplicationCore.DTOs.Basket;

public class TransferDTO
{
    public TransferDTO(string anonymousId, string userName)
    {
        AnonymousId = anonymousId;
        UserName = userName;
    }

    public string AnonymousId { get; set; }
    public string UserName { get; set; }
}
