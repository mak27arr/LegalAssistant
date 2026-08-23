import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import App from '../App';

describe('App navigation shell', () => {
  it('renders both primary routes in the navigation', () => {
    render(
      <MemoryRouter initialEntries={['/ask']}>
        <App />
      </MemoryRouter>,
    );

    expect(screen.getByRole('link', { name: 'Documents' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ask' })).toBeInTheDocument();
  });
});
