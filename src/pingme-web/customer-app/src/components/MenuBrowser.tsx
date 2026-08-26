import type { CustomerMenu } from "../types";

interface MenuBrowserProps {
  menus: CustomerMenu[];
  onAddToCart: (productId: string, name: string, price: number) => void;
}

export function MenuBrowser({ menus, onAddToCart }: MenuBrowserProps) {
  return (
    <div>
      {menus.map((menu) => (
        <section key={menu.id}>
          <h2>{menu.name}</h2>
          {menu.categories
            .slice()
            .sort((a, b) => a.sortOrder - b.sortOrder)
            .map((category) => (
              <div key={category.id}>
                <h3>{category.name}</h3>
                <ul>
                  {category.products.map((product) => (
                    <li key={product.id}>
                      <span>{product.name}</span>
                      <span> €{product.price.toFixed(2)}</span>
                      <button onClick={() => onAddToCart(product.id, product.name, product.price)}>
                        Add
                      </button>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
        </section>
      ))}
    </div>
  );
}
