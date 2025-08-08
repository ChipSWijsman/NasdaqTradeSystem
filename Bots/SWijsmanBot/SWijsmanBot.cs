using NasdaqTrader.Bot.Core;
using System.Collections.ObjectModel;
using System.Runtime.Intrinsics.Arm;

namespace ExampleTraderBot;

public class SWijsmanBot : ITraderBot
{
    public string CompanyName => "What is a stock";

	public async Task DoTurn(ITraderSystemContext systemContext)
	{
		try
		{
			#region Previous
			/*
			var listings = systemContext.GetListings();

			var tradesLeft = systemContext.GetTradesLeftForToday(this);
			foreach (var stockStatus in (_stocks.OrderBy(stock => systemContext.GetPriceOnDay(stock.Holding.Listing)).Reverse()).ToArray())
			{
				stockStatus.Update(systemContext.CurrentDate);
				if (tradesLeft > 0)
				{
					systemContext.SellStock(this, stockStatus.Holding.Listing, stockStatus.Amount);
					_stocks.Remove(stockStatus);
					tradesLeft = systemContext.GetTradesLeftForToday(this);
				}
			}

			tradesLeft = systemContext.GetTradesLeftForToday(this);
			while (tradesLeft > 0)
			{
				int range = (int)Math.Round(listings.Count * 0.1);
				var currentCash = systemContext.GetCurrentCash(this);
				var currentDate = systemContext.CurrentDate;
				var targetListing = GetOptimalListing(range, listings, systemContext.GetCurrentCash(this), currentDate, _stocks);
				if (targetListing == null)
				{
					tradesLeft = 0;
					continue;
				}
				decimal price = systemContext.GetPriceOnDay(targetListing);
				var amount = (int)Math.Floor(Math.Min(1000, systemContext.GetCurrentCash(this)) / price);
				var bought = systemContext.BuyStock(this, targetListing, amount);
				if (bought)
				{
					var purchasedHolding = systemContext.GetHolding(this, targetListing);
					_stocks.Add(new StockStatus(purchasedHolding, amount, systemContext.CurrentDate));
					tradesLeft = systemContext.GetTradesLeftForToday(this);
				}
				else
				{
					tradesLeft = 0;
				}
			}
			*/
			#endregion

			foreach (IHolding holding in systemContext.GetHoldings(this))
			{
				systemContext.SellStock(this, holding.Listing, holding.Amount);
			}

			List<IStockListing> listingToIgnore = new List<IStockListing>();

			int tradesLeft = systemContext.GetTradesLeftForToday(this);
			while (tradesLeft > 0)
			{
				var ok = false;
				do
				{
					IStockListing stockListing = FindInterestingListing(systemContext.GetListings(), systemContext.CurrentDate, listingToIgnore);
					if (stockListing == null)
					{
						Console.WriteLine("No listing found!");
						return;
					}

					decimal price = systemContext.GetPriceOnDay(stockListing);
					if (price == 0)
					{
						Console.WriteLine("Price is 0!");
						return;
					}

					int amount = (int)Math.Floor(systemContext.GetCurrentCash(this) / price);
					ok = systemContext.BuyStock(this, stockListing, amount);

					if (!ok)
					{
						listingToIgnore.Add(stockListing);
					}
				} while (!ok);
				tradesLeft = systemContext.GetTradesLeftForToday(this);
			}
		}
		catch (Exception e)
		{
			Console.WriteLine("___________________________");
			Console.WriteLine("Exception in SWijsmanBot: ");
			Console.WriteLine(e.ToString());
			Console.WriteLine(e.StackTrace);
			Console.WriteLine("___________________________");
		}
	}

	private decimal GetPriceDifference(ReadOnlySpan<IPricePoint> pricePoints, DateOnly date)
	{
		if (pricePoints.Length == 0)
		{
			return 0;
		}

		for (int i = 0; i < pricePoints.Length; i++)
		{
			if(i+1 == pricePoints.Length)
			{
				continue;
			}

			IPricePoint todaysPricePoint = pricePoints[i];
			DateOnly pricePointDate = todaysPricePoint.Date;

			if(i == 0 && pricePointDate > date)
			{
				return 0;
			}

			if(pricePointDate != date)
			{
				continue;
			}

			IPricePoint tomorrowsPricePoint = pricePoints[i+1];

			return tomorrowsPricePoint.Price - todaysPricePoint.Price;
		}

		return 0;
	}

	private IStockListing FindInterestingListing(ReadOnlyCollection<IStockListing> listings, DateOnly date, List<IStockListing> listingsToIgnore)
	{
		List<IStockListing> cleanedListings = listings.ToList();
		foreach (IStockListing item in listingsToIgnore)
		{
			cleanedListings.Remove(item);
		}

		ReadOnlySpan<IStockListing> availableListings = cleanedListings.ToArray().AsSpan();

		decimal max = 0;
		IStockListing maxDifferenceListing = null;

		for (int i = 0; i < availableListings.Length; i++)
		{
			IStockListing listing = availableListings[i];
			ReadOnlySpan<IPricePoint> pricePoints = listing.PricePoints.AsSpan();
			decimal difference = GetPriceDifference(pricePoints, date);
			if (difference > max)
			{
				max = difference;
				maxDifferenceListing = listing;
			}
		}
		
		return maxDifferenceListing;
	}

	/*
	private IStockListing GetOptimalListing(int range, IReadOnlyCollection<IStockListing> listings, decimal maxPrice, DateOnly today, List<StockStatus> currentStocks)
	{
		var todaysListings = listings
			.Where(listing =>
				listing.PricePoints.Length > 0 &&
				listing.PricePoints.Any(pricePoint => pricePoint.Date == today));

		if (todaysListings.Count() == 0)
		{
			return null;
		}


		var limitedListings = todaysListings
			.Where(listing => listing.PricePoints.First(pricePoint => pricePoint.Date == today).Price <= Math.Min(1000, maxPrice));

		if (limitedListings.Count() == 0)
		{
			return null;
		}

		// Hendrik Cheat

		var withTomorrowPricePoints = limitedListings
			.Where(listing => listing.PricePoints.Any(pricePoint => pricePoint.Date == today.AddDays(1)));

		if(withTomorrowPricePoints.Count() == 0)
		{
			return null;
		}

		var withGains = limitedListings
			.Where(listing => listing.PricePoints.First(pricePoint => pricePoint.Date == today.AddDays(1)).Price >
							listing.PricePoints.First(pricePoint => pricePoint.Date == today).Price);

		if(withGains.Count() == 0)
		{
			return null;
		}

		//

		var notOwnedListings = withGains
			.Where(listing => !currentStocks.Any(stock => stock.Holding.Listing == listing));
		

		if (notOwnedListings.Count() == 0)
		{
			return null;
		}

		var orderedListings = notOwnedListings
		.OrderBy(listing => listing.PricePoints.First(pricePoint => pricePoint.Date == today).Price)
		.Reverse()
		.ToArray();

		if (orderedListings.Length == 0)
		{
			return null;
		}

		if (range > orderedListings.Length)
		{
			range = orderedListings.Length;
		}

		int selection = (int)random.NextInt64(range);

		return orderedListings[selection];
	}
	*/
}