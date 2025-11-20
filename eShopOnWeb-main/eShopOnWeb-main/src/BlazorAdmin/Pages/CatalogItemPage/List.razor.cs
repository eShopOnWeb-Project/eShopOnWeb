using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlazorAdmin.Helpers;
using BlazorAdmin.Interfaces;
using BlazorAdmin.Models;
using BlazorAdmin.Services;
using BlazorShared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.eShopWeb.Infrastructure.RabbitMQ.DTO;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace BlazorAdmin.Pages.CatalogItemPage;

public partial class List : BlazorComponent
{
    [Inject] public ICatalogItemService CatalogItemService { get; set; }
    [Inject] public ICatalogBrandService CatalogBrandService { get; set; }
    [Inject] public ICatalogTypeService CatalogTypeService { get; set; }
    [Inject] public NavigationManager NavigationManager { get; set; }


    private HubConnection? hubConnection;
    private List<CatalogItem> catalogItems = new();
    private List<CatalogType> catalogTypes = new();
    private List<CatalogBrand> catalogBrands = new();

    private Edit EditComponent { get; set; }
    private Delete DeleteComponent { get; set; }
    private Details DetailsComponent { get; set; }
    private Create CreateComponent { get; set; }

    private Dictionary<int, int> restockAmounts = new();

    protected override async Task OnInitializedAsync()
    {
        hubConnection = new HubConnectionBuilder()
               .WithUrl(NavigationManager.ToAbsoluteUri("/stockhub"))
               .Build();

        hubConnection.On<RabbitMQFullDTOItem>("StockUpdated", item =>
        {
            var existing = catalogItems.Find(ci => ci.Id == item.itemId);
            if (existing != null)
            {
                existing.Total = item.total;
                existing.Reserved = item.reserved;
                InvokeAsync(StateHasChanged);
            }
        });

        await hubConnection.StartAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await ReloadCatalogItemsAndStock();
        }

        await base.OnAfterRenderAsync(firstRender);
    }

                                                  
    private async Task RestockClick(int itemId)
    {
        if (restockAmounts.TryGetValue(itemId, out var amount))
        {
            if (hubConnection != null)
            {
                await hubConnection.SendAsync("Restock", itemId, amount);

                restockAmounts[itemId] = 1;
            }
        }
    }

    public bool IsConnected =>
        hubConnection?.State == HubConnectionState.Connected;

    public async ValueTask DisposeAsync()
    {
        if (hubConnection is not null)
        {
            await hubConnection.DisposeAsync();
        }
    }


    private async void DetailsClick(int id) => await DetailsComponent.Open(id);
    private async Task CreateClick() => await CreateComponent.Open();
    private async Task EditClick(int id) => await EditComponent.Open(id);
    private async Task DeleteClick(int id) => await DeleteComponent.Open(id);

    private async Task ReloadCatalogItemsAndStock()
    {
        catalogItems = await CatalogItemService.List();
        catalogTypes = await CatalogTypeService.List();
        catalogBrands = await CatalogBrandService.List();

        foreach (var item in catalogItems)
            restockAmounts[item.Id] = 1;

        if (hubConnection != null)
        {
            try
            {
                var fullStock = await hubConnection.InvokeAsync<List<RabbitMQFullDTOItem>>("GetStockCacheAsync");
                if (fullStock != null)
                {
                    foreach (var stock in fullStock)
                    {
                        var existing = catalogItems.FirstOrDefault(ci => ci.Id == stock.itemId);
                        if (existing != null)
                        {
                            existing.Total = stock.total;
                            existing.Reserved = stock.reserved;
                        }
                    }
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching initial stock: {ex.Message}");
            }
        }

        CallRequestRefresh();
    }
}
