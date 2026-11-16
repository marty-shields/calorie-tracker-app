# Open Food Facts fixtures

Saved responses of `GET https://world.openfoodfacts.org/api/v2/product/{barcode}?fields=product_name,brands,nutriments,product_quantity_unit,serving_quantity_unit,categories_tags,serving_size,serving_quantity`, captured 2026-11-14 with one request each (the drink file again 2026-11-15 after `categories_tags` was added to the request, and the pizza, drink and solid files with the serving fields on 2026-11-15). Tests never call the live service. The type comes from `categories_tags` when it has tags (`en:beverages` means Drink), and from the `ml` unit only when it has none.

| File | Barcode | Notes |
| --- | --- | --- |
| solid-3017620422003.json | 3017620422003 | Nutella: grams, two brands, no `en:beverages` category (Food), no serving (`serving_size` and `serving_quantity` absent) |
| pizza-5060198643156.json | 5060198643156 | Margherita pizza: grams, `serving_size` `0.5 pizza (201.8 g)`, `serving_quantity` 201.8, `serving_quantity_unit` `g` |
| drink-5449000000996.json | 5449000000996 | Coca-Cola can: `serving_quantity_unit` is `ml` and the categories include `en:beverages` (Drink), `serving_size` `1 portion (330 ml)`, `serving_quantity` 330 |
| details-nutella-3017620422003.json | 3017620422003 | Nutella with the identifying fields (captured 2026-11-16, request below): `brands` `Ferrero, Nutella`, `generic_name` `Hazelnut And Cocoa Spread`, `quantity` `400 g e`, `product_quantity` 400, `pnns_groups_2` `Sweets`, `unique_scans_n` 804, `completeness` 0.9, `last_modified_t` 1794233882, no serving |
| details-cola-can-5000112637922.json | 5000112637922 | Coca-Cola can: `product_quantity` 330 with `product_quantity_unit` `ml`, Swedish `generic_name`, a serving in grams (`330 g`) that does not match the ml unit |
| details-mars-5000159407236.json | 5000159407236 | Mars bar: `product_quantity` 51, `serving_size` `100 g`, and `en:serving-quantity-over-product-quantity` in `data_quality_warnings_tags`, so no serving is stored |
| details-weetabix-5010029000023.json | 5010029000023 | Weetabix: `serving_size` `37.5 gram`, `pnns_groups_2` `Breakfast cereals`, `product_quantity` 430 |
| missing-0000000000017.json | 0000000000017 | HTTP 200, `status` 0 (no such product) |
| not-food-8410757001090.json | 8410757001090 | HTTP 404, `status` 0 (a product of another type) |
| missing-values-6111246651261.json | 6111246651261 | HTTP 200, `status` 1 (product found) but `product` is empty: no name or nutriments (captured 2026-11-14) |

The four `details-*` files were captured 2026-11-16 with one request each, using the current field list: `GET https://world.openfoodfacts.org/api/v2/product/{barcode}?fields=product_name,brands,nutriments,product_quantity_unit,serving_quantity_unit,categories_tags,serving_size,serving_quantity,generic_name,quantity,product_quantity,pnns_groups_2,unique_scans_n,completeness,last_modified_t,data_quality_warnings_tags`. The service also returned a few keys that were not asked for; they are ignored. No image field is requested. The older files above were captured with the shorter field list and stay as they were.
