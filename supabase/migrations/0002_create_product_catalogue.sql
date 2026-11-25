-- ============================================================================
-- 0002_create_product_catalogue
--
-- Creates the product catalogue read-side schema: `categories`, `brands`,
-- and `products`. RLS is the only real security boundary, per
-- ARCHITECTURE.md §Security — only published products are visible to the
-- public storefront; categories/brands are public reference data.
--
-- Companion plan: .specs/product-catalogue/plan.md §10
-- ============================================================================

CREATE TABLE IF NOT EXISTS categories (
    id      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name    TEXT NOT NULL,
    slug    TEXT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS brands (
    id      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name    TEXT NOT NULL,
    slug    TEXT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS products (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name                    TEXT NOT NULL,
    description             TEXT NOT NULL DEFAULT '',
    image_url               TEXT,
    original_price          NUMERIC(10,2) NOT NULL CHECK (original_price >= 0),
    sale_price              NUMERIC(10,2) CHECK (sale_price IS NULL OR sale_price >= 0),
    currency                TEXT NOT NULL DEFAULT 'CAD',
    category_id             UUID NOT NULL REFERENCES categories(id),
    brand_id                UUID NOT NULL REFERENCES brands(id),
    flavour                 TEXT,
    nicotine_strength_mg    INTEGER CHECK (nicotine_strength_mg IS NULL OR nicotine_strength_mg >= 0),
    stock_quantity          INTEGER NOT NULL DEFAULT 0 CHECK (stock_quantity >= 0),
    is_published            BOOLEAN NOT NULL DEFAULT TRUE,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT sale_not_above_original CHECK (sale_price IS NULL OR sale_price <= original_price)
);

CREATE INDEX IF NOT EXISTS idx_products_published  ON products(is_published);
CREATE INDEX IF NOT EXISTS idx_products_category   ON products(category_id);
CREATE INDEX IF NOT EXISTS idx_products_brand      ON products(brand_id);
CREATE INDEX IF NOT EXISTS idx_products_created_at ON products(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_products_price      ON products(original_price);

-- ============================================================================
-- Row-Level Security
-- ============================================================================
ALTER TABLE categories ENABLE ROW LEVEL SECURITY;
ALTER TABLE brands     ENABLE ROW LEVEL SECURITY;
ALTER TABLE products   ENABLE ROW LEVEL SECURITY;

-- Public storefront read: anyone (anon or authenticated) may read reference data.
CREATE POLICY "categories_public_read" ON categories FOR SELECT USING (true);
CREATE POLICY "brands_public_read"     ON brands     FOR SELECT USING (true);

-- Public storefront read: only PUBLISHED products are visible to the storefront.
CREATE POLICY "products_public_read" ON products
    FOR SELECT USING (is_published = true);

-- No INSERT/UPDATE/DELETE policies → writes are denied to storefront roles.
-- Admin product management (and any write policy) is a separate future feature.

-- ============================================================================
-- Seed data — ~18 vape products across categories/brands/flavours/strengths,
-- including discounted, out-of-stock, and one image-less row.
-- ============================================================================
INSERT INTO categories (name, slug) VALUES
    ('Disposables',  'disposables'),
    ('Pod Systems',  'pod-systems'),
    ('E-Liquids',    'e-liquids'),
    ('Mods',         'mods'),
    ('Accessories',  'accessories');

INSERT INTO brands (name, slug) VALUES
    ('Elf Bar',    'elf-bar'),
    ('Geek Bar',   'geek-bar'),
    ('Vaporesso',  'vaporesso'),
    ('SMOK',       'smok'),
    ('Naked 100',  'naked-100'),
    ('Juul',       'juul');

INSERT INTO products
    (name, description, image_url, original_price, sale_price, category_id, brand_id, flavour, nicotine_strength_mg, stock_quantity, is_published)
VALUES
    ('Elf Bar BC5000',
     'A long-lasting disposable vape with smooth airflow.',
     'https://placehold.co/400x400?text=Elf+Bar+BC5000',
     24.99, NULL,
     (SELECT id FROM categories WHERE slug = 'disposables'), (SELECT id FROM brands WHERE slug = 'elf-bar'),
     'Blue Razz Ice', 50, 40, true),

    ('Elf Bar BC5000 Watermelon Ice',
     'A long-lasting disposable vape with smooth airflow.',
     'https://placehold.co/400x400?text=Elf+Bar+BC5000',
     24.99, 19.99,
     (SELECT id FROM categories WHERE slug = 'disposables'), (SELECT id FROM brands WHERE slug = 'elf-bar'),
     'Watermelon Ice', 50, 35, true),

    ('Elf Bar BC5000 Ultra Peach Ice',
     'The extended-capacity version of the BC5000 lineup.',
     'https://placehold.co/400x400?text=Elf+Bar+Ultra',
     26.99, NULL,
     (SELECT id FROM categories WHERE slug = 'disposables'), (SELECT id FROM brands WHERE slug = 'elf-bar'),
     'Peach Ice', 50, 33, true),

    ('Geek Bar Pulse Mango',
     'A disposable vape with a dual-mesh coil for bold flavour.',
     'https://placehold.co/400x400?text=Geek+Bar+Pulse',
     27.99, NULL,
     (SELECT id FROM categories WHERE slug = 'disposables'), (SELECT id FROM brands WHERE slug = 'geek-bar'),
     'Mango', 50, 20, true),

    ('Geek Bar Pulse Strawberry Kiwi',
     'A disposable vape with a dual-mesh coil for bold flavour.',
     'https://placehold.co/400x400?text=Geek+Bar+Pulse',
     27.99, 22.99,
     (SELECT id FROM categories WHERE slug = 'disposables'), (SELECT id FROM brands WHERE slug = 'geek-bar'),
     'Strawberry Kiwi', 50, 15, true),

    ('Vaporesso XROS 3',
     'A compact refillable pod system with adjustable airflow.',
     NULL,
     34.99, NULL,
     (SELECT id FROM categories WHERE slug = 'pod-systems'), (SELECT id FROM brands WHERE slug = 'vaporesso'),
     NULL, NULL, 25, true),

    ('SMOK Nord 5',
     'A refillable pod kit with a 2000mAh battery.',
     'https://placehold.co/400x400?text=SMOK+Nord+5',
     39.99, NULL,
     (SELECT id FROM categories WHERE slug = 'pod-systems'), (SELECT id FROM brands WHERE slug = 'smok'),
     NULL, NULL, 0, true),

    ('Juul Device Kit',
     'The original closed-pod vaping system.',
     'https://placehold.co/400x400?text=Juul+Device',
     44.99, NULL,
     (SELECT id FROM categories WHERE slug = 'pod-systems'), (SELECT id FROM brands WHERE slug = 'juul'),
     NULL, NULL, 12, true),

    ('Juul Pods 4-Pack Mint',
     'Replacement pods compatible with Juul devices.',
     'https://placehold.co/400x400?text=Juul+Pods',
     15.99, NULL,
     (SELECT id FROM categories WHERE slug = 'pod-systems'), (SELECT id FROM brands WHERE slug = 'juul'),
     'Mint', 30, 55, true),

    ('Naked 100 Lava Flow E-Liquid',
     'A 60ml bottle of tropical fruit e-liquid.',
     'https://placehold.co/400x400?text=Naked+100',
     19.99, NULL,
     (SELECT id FROM categories WHERE slug = 'e-liquids'), (SELECT id FROM brands WHERE slug = 'naked-100'),
     'Lava Flow', 3, 50, true),

    ('Naked 100 Green Blast E-Liquid',
     'A 60ml bottle of green apple e-liquid.',
     'https://placehold.co/400x400?text=Naked+100',
     19.99, 15.99,
     (SELECT id FROM categories WHERE slug = 'e-liquids'), (SELECT id FROM brands WHERE slug = 'naked-100'),
     'Green Blast', 6, 45, true),

    ('Naked 100 Amazing Mango E-Liquid',
     'A 60ml bottle of ripe mango e-liquid.',
     'https://placehold.co/400x400?text=Naked+100',
     19.99, NULL,
     (SELECT id FROM categories WHERE slug = 'e-liquids'), (SELECT id FROM brands WHERE slug = 'naked-100'),
     'Amazing Mango', 12, 30, true),

    ('Vaporesso Cool Mint E-Liquid',
     'A 30ml bottle of crisp mint e-liquid.',
     'https://placehold.co/400x400?text=Vaporesso+E-Liquid',
     17.99, NULL,
     (SELECT id FROM categories WHERE slug = 'e-liquids'), (SELECT id FROM brands WHERE slug = 'vaporesso'),
     'Cool Mint', 20, 22, true),

    ('SMOK RPM 5 Mod Kit',
     'A pod mod with adjustable wattage up to 80W.',
     'https://placehold.co/400x400?text=SMOK+RPM+5',
     49.99, NULL,
     (SELECT id FROM categories WHERE slug = 'mods'), (SELECT id FROM brands WHERE slug = 'smok'),
     NULL, NULL, 18, true),

    ('Vaporesso Gen 200 Mod',
     'A dual-battery mod with a 5.5ml top-fill tank.',
     'https://placehold.co/400x400?text=Vaporesso+Gen+200',
     54.99, 44.99,
     (SELECT id FROM categories WHERE slug = 'mods'), (SELECT id FROM brands WHERE slug = 'vaporesso'),
     NULL, NULL, 10, true),

    ('Elf Bar Replacement Coils',
     'A 5-pack of replacement coils.',
     'https://placehold.co/400x400?text=Elf+Bar+Coils',
     9.99, NULL,
     (SELECT id FROM categories WHERE slug = 'accessories'), (SELECT id FROM brands WHERE slug = 'elf-bar'),
     NULL, NULL, 60, true),

    ('Geek Bar Charging Cable',
     'A USB-C fast-charging cable.',
     'https://placehold.co/400x400?text=Geek+Bar+Cable',
     6.99, NULL,
     (SELECT id FROM categories WHERE slug = 'accessories'), (SELECT id FROM brands WHERE slug = 'geek-bar'),
     NULL, NULL, 100, true),

    ('SMOK Glass Replacement Tank',
     'A spare glass tank section for SMOK pod kits.',
     'https://placehold.co/400x400?text=SMOK+Glass+Tank',
     12.99, 9.99,
     (SELECT id FROM categories WHERE slug = 'accessories'), (SELECT id FROM brands WHERE slug = 'smok'),
     NULL, NULL, 40, true);
