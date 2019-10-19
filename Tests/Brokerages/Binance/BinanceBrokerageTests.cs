﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Interfaces;
using QuantConnect.Securities;
using NUnit.Framework;
using QuantConnect.Brokerages.Binance;
using QuantConnect.Configuration;
using Moq;
using QuantConnect.Brokerages;
using QuantConnect.Tests.Common.Securities;
using QuantConnect.Orders;
using QuantConnect.Logging;
using System.Threading;

namespace QuantConnect.Tests.Brokerages.Binance
{
    [TestFixture, Ignore("This test requires a configured and testable Binance practice account")]
    public partial class BinanceBrokerageTests : BrokerageTests
    {
        private BinanceRestApiClient _bibanceApi;

        /// <summary>
        /// Creates the brokerage under test and connects it
        /// </summary>
        /// <param name="orderProvider"></param>
        /// <param name="securityProvider"></param>
        /// <returns></returns>
        protected override IBrokerage CreateBrokerage(IOrderProvider orderProvider, ISecurityProvider securityProvider)
        {
            var securities = new SecurityManager(new TimeKeeper(DateTime.UtcNow, new[] { TimeZones.NewYork }));
            securities.Add(Symbol, CreateSecurity(Symbol));
            var transactions = new SecurityTransactionManager(null, securities);
            transactions.SetOrderProcessor(new FakeOrderProcessor());

            var algorithm = new Mock<IAlgorithm>();
            algorithm.Setup(a => a.Transactions).Returns(transactions);
            algorithm.Setup(a => a.BrokerageModel).Returns(new BinanceBrokerageModel(AccountType.Cash));
            algorithm.Setup(a => a.Portfolio).Returns(new SecurityPortfolioManager(securities, transactions));

            var priceProvider = new Mock<IPriceProvider>();
            priceProvider.Setup(a => a.GetLastPrice(It.IsAny<Symbol>())).Returns(1.234m);

            var wssUrl = Config.Get("binance-wss", "wss://stream.binance.com:9443");
            var restUrl = Config.Get("binance-rest", "https://api.binance.com");
            var apiKey = Config.Get("binance-api-key");
            var apiSecret = Config.Get("binance-api-secret");

            _bibanceApi = new BinanceRestApiClient(
                new BinanceSymbolMapper(),
                algorithm.Object?.Portfolio,
                restUrl,
                apiKey,
                apiSecret);

            return new BinanceBrokerage(
                    wssUrl,
                    restUrl,
                    apiKey,
                    apiSecret,
                    algorithm.Object,
                    priceProvider.Object
                );
        }

        /// <summary>
        /// Gets Binance symbol mapper
        /// </summary>
        protected ISymbolMapper SymbolMapper => new BinanceSymbolMapper();

        /// <summary>
        /// Gets the symbol to be traded, must be shortable
        /// </summary>
        protected override Symbol Symbol => Symbol.Create("ETHUSDT", SecurityType.Crypto, Market.Binance);

        /// <summary>
        /// Gets the security type associated with the <see cref="BrokerageTests.Symbol" />
        /// </summary>
        protected override SecurityType SecurityType => SecurityType.Crypto;

        //no stop limit support in v1
        public override TestCaseData[] OrderParameters => new[]
        {
            new TestCaseData(new MarketOrderTestParameters(Symbol)).SetName("MarketOrder"),
            new TestCaseData(new LimitOrderTestParameters(Symbol, HighPrice, LowPrice)).SetName("LimitOrder"),
            new TestCaseData(new StopLimitOrderTestParameters(Symbol, HighPrice, LowPrice)).SetName("StopLimitOrder"),
        };

        /// <summary>
        /// Gets a high price for the specified symbol so a limit sell won't fill
        /// </summary>
        protected override decimal HighPrice => 300m;

        /// <summary>
        /// Gets a low price for the specified symbol so a limit buy won't fill
        /// </summary>
        protected override decimal LowPrice => 100m;

        /// <summary>
        /// Gets the current market price of the specified security
        /// </summary>
        protected override decimal GetAskPrice(Symbol symbol)
        {
            var prices = _bibanceApi.GetTickers();
            return prices
                .FirstOrDefault(t => t.Symbol == SymbolMapper.GetBrokerageSymbol(symbol))
                .Price;
        }

        /// <summary>
        /// Returns wether or not the brokers order methods implementation are async
        /// </summary>
        protected override bool IsAsync() => false;

        /// <summary>
        /// Gets the default order quantity. Min order 10USD.
        /// </summary>
        protected override decimal GetDefaultQuantity() => 0.1m;

        [Test, Ignore("Holdings are now set to 0 swaps at the start of each launch. Not meaningful.")]
        public override void GetAccountHoldings()
        {
            Log.Trace("");
            Log.Trace("GET ACCOUNT HOLDINGS");
            Log.Trace("");
            var before = Brokerage.GetAccountHoldings();
            Assert.AreEqual(0, before.Count());

            PlaceOrderWaitForStatus(new MarketOrder(Symbol, GetDefaultQuantity(), DateTime.Now));
            Thread.Sleep(3000);

            var after = Brokerage.GetAccountHoldings();
            Assert.AreEqual(0, after.Count());
        }

        protected override void ModifyOrderUntilFilled(Order order, OrderTestParameters parameters, double secondsTimeout = 90)
        {
            Assert.Pass("Order update not supported. Please cancel and re-create.");
        }
    }
}
