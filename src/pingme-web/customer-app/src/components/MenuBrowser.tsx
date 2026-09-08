import { formatMoney } from "../format";
import type { CartItem } from "../cart";
import { quantityOf } from "../cart";
import type { CustomerMenu, CustomerProduct } from "../types";

interface MenuBrowserProps {
  menus: CustomerMenu[];
  cart: CartItem[];
  currencyCode: string;
  onAdd: (product: CustomerProduct) => void;
  onRemoveOne: (productId: string) => void;
}

export function MenuBrowser({
  menus,
  cart,
  currencyCode,
  onAdd,
  onRemoveOne,
}: MenuBrowserProps) {
  const hasAnyProduct = menus.some((menu) =>
    menu.categories.some((category) => category.products.length > 0),
  );

  if (!hasAnyProduct) {
    return (
      <p className="empty">
        Nothing is available to order here right now. Ask a staff member if you need something.
      </p>
    );
  }

  return (
    <div className="menu">
      {menus.map((menu) => {
        const categories = menu.categories
          .filter((category) => category.products.length > 0)
          .slice()
          .sort((a, b) => a.sortOrder - b.sortOrder);

        if (categories.length === 0) {
          return null;
        }

        return (
          <section key={menu.id} className="menu-section" aria-labelledby={`menu-${menu.id}`}>
            <h2 className="menu-section__title" id={`menu-${menu.id}`}>
              {menu.name}
            </h2>

            {categories.map((category) => (
              <div className="category" key={category.id}>
                <h3 className="category__label">{category.name}</h3>
                <ul>
                  {category.products.map((product) => (
                    <ProductRow
                      key={product.id}
                      product={product}
                      quantity={quantityOf(cart, product.id)}
                      currencyCode={currencyCode}
                      onAdd={onAdd}
                      onRemoveOne={onRemoveOne}
                    />
                  ))}
                </ul>
              </div>
            ))}
          </section>
        );
      })}
    </div>
  );
}

interface ProductRowProps {
  product: CustomerProduct;
  quantity: number;
  currencyCode: string;
  onAdd: (product: CustomerProduct) => void;
  onRemoveOne: (productId: string) => void;
}

function ProductRow({ product, quantity, currencyCode, onAdd, onRemoveOne }: ProductRowProps) {
  const price = formatMoney(product.price, currencyCode);

  return (
    <li className={`product${product.imageUrl ? " product--with-image" : ""}`}>
      {product.imageUrl && (
        <img
          className="product__thumb"
          src={product.imageUrl}
          alt=""
          aria-hidden="true"
          width={56}
          height={56}
          loading="lazy"
        />
      )}

      <div className="product__body">
        <p className="product__name">{product.name}</p>
        {product.description && <p className="product__description">{product.description}</p>}
        <p className="product__price">{price}</p>
      </div>

      {quantity === 0 ? (
        <button
          type="button"
          className="btn btn--add"
          onClick={() => onAdd(product)}
          aria-label={`Add ${product.name}, ${price}`}
        >
          <span aria-hidden="true">+</span>
        </button>
      ) : (
        <div className="stepper">
          <button
            type="button"
            className="stepper__btn"
            onClick={() => onRemoveOne(product.id)}
            aria-label={`Remove one ${product.name}`}
          >
            <span aria-hidden="true">&minus;</span>
          </button>
          <span className="stepper__count" aria-live="polite">
            <span className="visually-hidden">{product.name} quantity: </span>
            {quantity}
          </span>
          <button
            type="button"
            className="stepper__btn"
            onClick={() => onAdd(product)}
            aria-label={`Add another ${product.name}`}
          >
            <span aria-hidden="true">+</span>
          </button>
        </div>
      )}
    </li>
  );
}
