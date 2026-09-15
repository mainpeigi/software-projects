window.authStorage = {
    setSession: function(accessToken, refreshToken) {
        localStorage.setItem('supabase_access_token', accessToken);
        localStorage.setItem('supabase_refresh_token', refreshToken);
    },
    getSession: function() {
        return {
            accessToken: localStorage.getItem('supabase_access_token'),
            refreshToken: localStorage.getItem('supabase_refresh_token')
        };
    },
    clearSession: function() {
        localStorage.removeItem('supabase_access_token');
        localStorage.removeItem('supabase_refresh_token');
    }
};