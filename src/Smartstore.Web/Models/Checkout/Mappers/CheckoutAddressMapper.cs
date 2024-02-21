﻿using System.Dynamic;
using Smartstore.ComponentModel;
using Smartstore.Core.Localization;
using Smartstore.Core.Stores;
using Smartstore.Web.Models.Checkout;
using Smartstore.Web.Models.Common;
using Smartstore.Web.Rendering;

namespace Smartstore.Web.Models.Checkout
{
    public static partial class CheckoutAddressMappingExtensions
    {
        public static async Task<CheckoutAddressModel> MapAsync(this IEnumerable<Address> entities,
            bool shipping,
            int? selectedCountryId = null)
        {
            var model = new CheckoutAddressModel();
            await entities.MapAsync(model, shipping, selectedCountryId);

            return model;
        }

        public static async Task MapAsync(this IEnumerable<Address> entities,
            CheckoutAddressModel model,
            bool shipping,
            int? selectedCountryId)
        {
            dynamic parameters = new ExpandoObject();
            parameters.SelectedCountryId = selectedCountryId;
            parameters.Shipping = shipping;

            await MapperFactory.MapAsync(entities, model, parameters);
        }
    }

    public class CheckoutAddressMapper : Mapper<IEnumerable<Address>, CheckoutAddressModel>
    {
        private readonly SmartDbContext _db;
        private readonly IStoreContext _storeContext;
        private readonly IWorkContext _workContext;
        
        public CheckoutAddressMapper(
            SmartDbContext db,
            IStoreContext storeContext,
            IWorkContext workContext)
        {
            _db = db;
            _storeContext = storeContext;
            _workContext = workContext;
        }

        public Localizer T { get; set; } = NullLocalizer.Instance;

        protected override void Map(IEnumerable<Address> from, CheckoutAddressModel to, dynamic parameters = null)
            => throw new NotImplementedException();

        public override async Task MapAsync(IEnumerable<Address> from, CheckoutAddressModel to, dynamic parameters = null)
        {
            Guard.NotNull(to);

            var shipping = parameters?.Shipping == true;
            var selectedCountryId = parameters?.SelectedCountryId as int?;

            from = shipping
                ? from.Where(x => x.Country == null || x.Country.AllowsShipping)
                : from.Where(x => x.Country == null || x.Country.AllowsBilling);

            foreach (var address in from)
            {
                to.ExistingAddresses.Add(await address.MapAsync());
            }

            // New address.
            var countriesQuery = _db.Countries.AsNoTracking();

            countriesQuery = shipping
                ? countriesQuery.Where(x => x.AllowsShipping)
                : countriesQuery.Where(x => x.AllowsBilling);

            var countries = await countriesQuery
                .ApplyStandardFilter(false, _storeContext.CurrentStore.Id)
                .ToListAsync();

            await new Address().MapAsync(to.NewAddress);

            to.NewAddress.CountryId = selectedCountryId;
            to.NewAddress.Email = _workContext.CurrentCustomer.Email;

            if (to.NewAddress.CountryEnabled)
            {
                to.NewAddress.AvailableCountries = countries.ToSelectListItems(selectedCountryId ?? 0);
            }
        }
    }
}
