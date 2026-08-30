import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import App from '../App';
import { AuthProvider } from '../features/auth/AuthContext';

describe('App navigation shell', () => {
  it('renders the sign-in route for anonymous users', async () => {
    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/login']}>
          <App />
        </MemoryRouter>
      </AuthProvider>,
    );

    expect(await screen.findByRole('link', { name: 'Sign in' })).toBeInTheDocument();
  });
});
