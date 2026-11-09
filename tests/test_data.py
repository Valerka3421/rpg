"""Тесты игровых данных."""
import random
from core.data import (ITEMS, BOSSES, scale_stats,
                       make_shop_stock, reroll_price)


def test_all_items_have_required_fields():
    for key, it in ITEMS.items():
        for f in ('name', 'ch', 'type', 'color', 'desc'):
            assert f in it, f"{key}: нет {f}"


def test_all_items_have_sell_price_except_gold():
    for key, it in ITEMS.items():
        if it['type'] == 'gold':
            continue
        assert 'sell' in it and it['sell'] >= 0, f"{key}: некорректный sell"


def test_books_have_shop_price():
    for key, it in ITEMS.items():
        if it['type'] == 'book':
            assert 'shop_price' in it and 'stat' in it


def test_sell_price_less_than_shop():
    for key, it in ITEMS.items():
        if it['type'] == 'book':
            assert it['sell'] < it['shop_price']


def test_scale_stats_increases():
    hp1, atk1, df1 = scale_stats(10, 3, 1, 1)
    hp5, atk5, df5 = scale_stats(10, 3, 1, 5)
    assert hp5 > hp1 and atk5 > atk1 and df5 >= df1


def test_make_shop_stock_returns_valid():
    random.seed(42)
    stock = make_shop_stock(3)
    assert len(stock) >= 4
    for e in stock:
        assert e['key'] in ITEMS and e['price'] > 0 and e['sold'] is False


def test_make_shop_stock_has_base_items():
    random.seed(1)
    stock = make_shop_stock(5)
    keys = [e['key'] for e in stock]
    assert sum(1 for k in keys if ITEMS[k]['type'] == 'heal') >= 2
    assert sum(1 for k in keys if ITEMS[k]['type'] == 'weapon') >= 1
    assert sum(1 for k in keys if ITEMS[k]['type'] == 'armor') >= 1


def test_shop_tier_scales_with_depth():
    random.seed(0)
    assert 'potion_s' in {e['key'] for e in make_shop_stock(2)}
    assert 'potion_l' in {e['key'] for e in make_shop_stock(9)}


def test_reroll_price_scales():
    assert reroll_price(3) == 24
    assert reroll_price(6) == 33
    assert reroll_price(3) < reroll_price(6)


def test_boss_floors_have_definitions():
    for d in (5, 10, 15, 20):
        assert d in BOSSES
    assert BOSSES[20]['name'] == 'Древний дракон'