-- Advisor follow-up: pin the trigger function's search_path, and cover the three
-- foreign keys the linter flagged as unindexed after migration 0022.
ALTER FUNCTION public.trg_refresh_product_read_model() SET search_path = public;

CREATE INDEX idx_product_sku_registry_variant ON product_sku_registry(variant_id);
CREATE INDEX idx_product_variant_option_values_option_value ON product_variant_option_values(option_value_id);
CREATE INDEX idx_product_variants_image ON product_variants(image_id);
