import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // Bind to every interface so a phone on the same Wi-Fi can open the app.
    // Vite then prints a Network URL alongside the localhost one.
    host: true,
  },
});
