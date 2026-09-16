import { BrowserRouter, Navigate, Route, Routes } from 'react-router';
import { AuthProvider } from '../features/auth/AuthProvider';
import { ChangePasswordPage } from '../features/auth/ChangePasswordPage';
import { ForbiddenPage } from '../features/auth/ForbiddenPage';
import { LoginPage } from '../features/auth/LoginPage';
import { NotFoundPage } from '../features/auth/NotFoundPage';
import { PERMISSIONS } from '../features/auth/permissions.ts';
import { RequireAuth } from '../features/auth/RequireAuth';
import { RequirePermission } from '../features/auth/RequirePermission';
import { ProductGroupDetailPage } from '../features/productGroups/ProductGroupDetailPage';
import { ProductGroupFormPage } from '../features/productGroups/ProductGroupFormPage';
import { ProductGroupListPage } from '../features/productGroups/ProductGroupListPage';
import { UserFormPage } from '../features/users/UserFormPage';
import { UserListPage } from '../features/users/UserListPage';
import { AppShell } from './AppShell';
import { SystemPage } from './SystemPage';
import { ThemeProvider } from './theme/ThemeProvider';

export function App() {
  return (
    <ThemeProvider>
      <BrowserRouter>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<LoginPage />} />

            <Route element={<RequireAuth />}>
              <Route element={<AppShell />}>
                <Route index element={<Navigate to="/product-groups" replace />} />
                <Route path="/change-password" element={<ChangePasswordPage />} />

                <Route
                  path="/product-groups"
                  element={
                    <RequirePermission permission={PERMISSIONS.productGroupView}>
                      <ProductGroupListPage />
                    </RequirePermission>
                  }
                />
                <Route
                  path="/product-groups/new"
                  element={
                    <RequirePermission permission={PERMISSIONS.productGroupManagement}>
                      <ProductGroupFormPage mode="create" />
                    </RequirePermission>
                  }
                />
                <Route
                  path="/product-groups/:id"
                  element={
                    <RequirePermission permission={PERMISSIONS.productGroupView}>
                      <ProductGroupDetailPage />
                    </RequirePermission>
                  }
                />
                <Route
                  path="/product-groups/:id/edit"
                  element={
                    <RequirePermission permission={PERMISSIONS.productGroupManagement}>
                      <ProductGroupFormPage mode="edit" />
                    </RequirePermission>
                  }
                />

                <Route
                  path="/admin/users"
                  element={
                    <RequirePermission permission={PERMISSIONS.userManagement}>
                      <UserListPage />
                    </RequirePermission>
                  }
                />
                <Route
                  path="/admin/users/new"
                  element={
                    <RequirePermission permission={PERMISSIONS.userManagement}>
                      <UserFormPage mode="create" />
                    </RequirePermission>
                  }
                />
                <Route
                  path="/admin/users/:id"
                  element={
                    <RequirePermission permission={PERMISSIONS.userManagement}>
                      <UserFormPage mode="edit" />
                    </RequirePermission>
                  }
                />

                <Route
                  path="/system"
                  element={
                    <RequirePermission permission={PERMISSIONS.userManagement}>
                      <SystemPage />
                    </RequirePermission>
                  }
                />

                <Route path="/forbidden" element={<ForbiddenPage />} />
                <Route path="*" element={<NotFoundPage />} />
              </Route>
            </Route>
          </Routes>
        </AuthProvider>
      </BrowserRouter>
    </ThemeProvider>
  );
}
