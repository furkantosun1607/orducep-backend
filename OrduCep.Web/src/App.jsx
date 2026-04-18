import { useState, useEffect } from 'react';
import axios from 'axios';

const API = '/api/reservations';
const AUTH_API = '/api/auth';
const ORDUEVI_API = '/api/orduevleri';
const USER_ID = "demo-user-123";

/* ─── Tesis bilgi yardımcısı (demo) ─── */
const getFacilityInfo = (name) => {
    const n = name.toLowerCase();
    if (n.includes('berber') || n.includes('kuaför'))
        return { image: '/barber.png', desc: 'Profesyonel ekibimizle saç ve sakal bakımınız için en iyi hizmeti sunuyoruz.', services: [{ name: 'Saç Kesimi', price: '45.00 TL', time: '15 dk' }, { name: 'Sakal Tıraşı', price: '25.00 TL', time: '10 dk' }, { name: 'Saç & Sakal Kesimi', price: '60.00 TL', time: '30 dk' }, { name: 'Saç Yıkama & Fön', price: '30.00 TL', time: '10 dk' }] };
    if (n.includes('pastane'))
        return { image: '/restaurant.png', desc: 'Günlük taze üretilen tatlılarımızla çay saatlerinize lüks katıyoruz.', services: [{ name: 'Dilim Yaş Pasta', price: '40.00 TL', time: '-' }, { name: 'Sütlü Tatlılar', price: '35.00 TL', time: '-' }, { name: 'Kuru Pasta', price: '30.00 TL', time: '-' }, { name: 'Çay / Kahve', price: '10.00 TL', time: '-' }] };
    if (n.includes('spor'))
        return { image: '/restaurant.png', desc: 'Modern spor aletleri ve hijyenik ortamıyla formunuzu korumanız için donatılmış spor salonumuz.', services: [{ name: 'Günlük Giriş', price: 'Ücretsiz', time: '-' }, { name: 'Aylık Üyelik', price: '150.00 TL', time: '-' }, { name: 'Havlu Temini', price: '10.00 TL', time: '-' }] };
    if (n.includes('bar'))
        return { image: '/restaurant.png', desc: 'Nezih ortam. Yerli, yabancı alkollü ve alkolsüz içecek servisimizle.', services: [{ name: 'Karışık Çerez', price: '30.00 TL', time: '-' }, { name: 'Yerli İçecekler', price: '50.00 TL', time: '-' }, { name: 'Yabancı İçecekler', price: '110.00 TL', time: '-' }, { name: 'Meyve Tabağı', price: '45.00 TL', time: '-' }] };
    return { image: '/restaurant.png', desc: 'Usta aşçılarımızın ellerinden taze hazırlanan menüler sizleri bekliyor.', services: [{ name: 'Günün Çorbası', price: '25.00 TL', time: '-' }, { name: 'Izgara / Et', price: '120.00 TL', time: '-' }, { name: 'Zeytinyağlı', price: '45.00 TL', time: '-' }, { name: 'Soğuk İçecekler', price: '20.00 TL', time: '-' }] };
};

/* ═══════════════════════════════════════════════════════════════════ */
export default function App() {
    /* ─── Auth State ─── */
    const [isAuthenticated, setIsAuthenticated] = useState(false);
    const [userRole, setUserRole] = useState(null);
    const [loggedInUser, setLoggedInUser] = useState(null);
    const [authMode, setAuthMode] = useState('login');
    const [loginIdentityNo, setLoginIdentityNo] = useState('');
    const [loginPassword, setLoginPassword] = useState('');
    const [registerLoading, setRegisterLoading] = useState(false);
    const [isRelative, setIsRelative] = useState(false);
    const [registerForm, setRegisterForm] = useState({
        identityNumber: '', password: '', firstName: '', lastName: '',
        relation: '', ownerTcNo: '', ownerFirstName: '', ownerLastName: '', ownerRank: ''
    });

    /* ─── Orduevi & Facility State ─── */
    const [globalOrduevi, setGlobalOrduevi] = useState(null);
    const [searchQuery, setSearchQuery] = useState('');
    const [facilities, setFacilities] = useState([]);
    const [currentView, setCurrentView] = useState('home');

    const [allOrduevis, setAllOrduevis] = useState([]);

    /* ─── Admin State ─── */
    const [adminSearchQuery, setAdminSearchQuery] = useState('');
    const [editingOrdueviId, setEditingOrdueviId] = useState(null);
    const [editingFacility, setEditingFacility] = useState(null);
    const [newOrdueviName, setNewOrdueviName] = useState('');
    const [newOrdueviLoc, setNewOrdueviLoc] = useState('');
    const [newOrdueviDesc, setNewOrdueviDesc] = useState('');
    const [newOrdueviContact, setNewOrdueviContact] = useState('');

    /* ─── Profile & Reservations State ─── */
    const [myReservations, setMyReservations] = useState([]);

    const filteredAdminOrduevis = allOrduevis.filter(o =>
        o.name?.toLowerCase().includes(adminSearchQuery.toLowerCase()) ||
        o.location?.toLowerCase().includes(adminSearchQuery.toLowerCase())
    );

    const generateDefaultFacilities = (ordId) => {
        if (!ordId) return [];
        if (ordId.includes('ankara')) {
            return [
                { id: `fac-${ordId}-1`, name: 'Erkek Berberi', isAppointmentBased: true, type: 'appointment', image: '/barber.png', desc: 'Profesyonel ekibimizle saç ve sakal bakımınız için en iyi hizmeti sunuyoruz.', services: [{ name: 'Saç Kesimi', price: '45' }, { name: 'Sakal Tıraşı', price: '25' }], staff: ['Svl. Memur Ahmet Yılmaz', 'Uzm. Çvş. Mehmet Öztürk'], hours: { weekdayOpen: '08:30', weekdayClose: '17:00', weekendOpen: '', weekendClose: '', weekendClosed: true, closedDays: ['Pazartesi'] } },
                { id: `fac-${ordId}-2`, name: 'Açık Teras Pide Salonu', isAppointmentBased: false, type: 'walk-in', image: '/restaurant.png', desc: 'Açık teras keyfi ile eşsiz lezzetler sizleri bekliyor.', services: [{ name: 'Kıymalı Pide', price: '85' }, { name: 'Kuşbaşılı Pide', price: '110' }], staff: ['Aşçı Hakan Demir'], hours: { weekdayOpen: '11:30', weekdayClose: '20:30', weekendOpen: '11:30', weekendClose: '22:00', weekendClosed: false, closedDays: [] } },
                { id: `fac-${ordId}-3`, name: 'Kapalı Spor Salonu', isAppointmentBased: true, type: 'appointment', image: '/restaurant.png', desc: 'Modern spor aletleriyle donatılmış spor salonumuz.', services: [{ name: 'Günlük Giriş', price: '0' }, { name: 'Aylık Üyelik', price: '150' }], staff: [], hours: { weekdayOpen: '07:00', weekdayClose: '22:00' } }
            ];
        } else if (ordId.includes('antalya') || ordId.includes('izmir') || ordId.includes('mugla')) {
            return [
                { id: `fac-${ordId}-3`, name: 'Bayan Kuaförü', isAppointmentBased: true, type: 'appointment', image: '/barber.png', desc: 'Deneyimli kuaförlerimizle...', services: [{ name: 'Saç Kesimi', price: '80' }], staff: [], hours: { weekdayOpen: '09:00', weekdayClose: '18:00' } },
                { id: `fac-${ordId}-4`, name: 'Açık Havuz Bar', isAppointmentBased: false, type: 'walk-in', image: '/restaurant.png', desc: 'Havuz kenarında serinletici içecekler...', services: [{ name: 'Limonata', price: '30' }], staff: [], hours: { weekdayOpen: '10:00', weekdayClose: '23:00' } }
            ];
        } else {
            return [
                { id: `fac-${ordId}-5`, name: 'Kantin', isAppointmentBased: false, type: 'walk-in', image: '/restaurant.png', desc: 'Günlük ihtiyaçlarınız için kantin.', services: [{ name: 'Çay', price: '10' }], staff: [], hours: { weekdayOpen: '08:00', weekdayClose: '22:00' } },
                { id: `fac-${ordId}-6`, name: 'Erkek Berberi', isAppointmentBased: true, type: 'appointment', image: '/barber.png', desc: 'Askeri personel ve misafirleri için uzman saç kesimi.', services: [{ name: 'Saç Kesimi', price: '45' }], staff: [], hours: { weekdayOpen: '09:00', weekdayClose: '17:00' } }
            ];
        }
    };

    const [globalFacilitiesMap, setGlobalFacilitiesMap] = useState({});
    const [newFacilityName, setNewFacilityName] = useState('');
    const [newFacilityIsAppointment, setNewFacilityIsAppointment] = useState('false');
    const [newFacilityOpen, setNewFacilityOpen] = useState('');
    const [newFacilityClose, setNewFacilityClose] = useState('');

    const handleAddFacility = async () => {
        if (!newFacilityName || !editingOrdueviId) return;
        try {
            const res = await axios.post(`${API}/facilities`, {
                ordueviId: editingOrdueviId,
                name: newFacilityName,
                isAppointmentBased: newFacilityIsAppointment === 'true',
                openingTime: newFacilityOpen || null,
                closingTime: newFacilityClose || null
            });
            const created = res.data;
            const facs = globalFacilitiesMap[editingOrdueviId] || generateDefaultFacilities(editingOrdueviId);
            const info = getFacilityInfo(created.name || '');
            const mapped = {
                id: created.id,
                name: created.name,
                isAppointmentBased: created.isAppointmentBased,
                type: created.isAppointmentBased ? 'appointment' : 'walk-in',
                image: info.image,
                desc: info.desc,
                services: info.services,
                staff: [],
                hours: {
                    weekdayOpen: safeTime(created.openingTime, newFacilityOpen || '08:00'),
                    weekdayClose: safeTime(created.closingTime, newFacilityClose || '17:00')
                }
            };
            setGlobalFacilitiesMap(prev => ({ ...prev, [editingOrdueviId]: [...facs, mapped] }));
            setNewFacilityName(''); setNewFacilityOpen(''); setNewFacilityClose('');
            alert('Tesis başarıyla eklendi!');
        } catch (err) {
            console.error('Tesis eklenirken hata:', err);
            alert('Tesis eklenirken bir hata oluştu. Lütfen tekrar deneyin.');
        }
    };

    /* ─── Slot / Booking State ─── */
    const [slots, setSlots] = useState([]);
    const [loading, setLoading] = useState(false);
    const [isSidebarOpen, setIsSidebarOpen] = useState(false);
    const [selectedSlot, setSelectedSlot] = useState(null);
    const [isModalOpen, setIsModalOpen] = useState(false);
    const [actionLoading, setActionLoading] = useState(false);

    const todayDate = new Date().toISOString().split('T')[0];

    const safeTime = (value, fallback) => {
        if (!value) return fallback;
        if (typeof value === 'string') {
            const match = value.match(/\d{2}:\d{2}/);
            return match ? match[0] : fallback;
        }
        return fallback;
    };

    /* ─── Effects ─── */
    useEffect(() => {
        const fetchOrduevleri = async () => {
            try {
                const res = await axios.get(ORDUEVI_API);
                const data = Array.isArray(res.data) ? res.data : [];
                setAllOrduevis(data);
            } catch (err) {
                console.error('Orduevleri yüklenirken hata:', err);
            }
        };

        fetchOrduevleri();
    }, []);

    useEffect(() => {
        if (globalOrduevi) { fetchFacilities(globalOrduevi.id); setCurrentView('home'); }
    }, [globalOrduevi]);

    /* Admin: Yönet ekranına girince tesisleri backend'den çek */
    useEffect(() => {
        if (userRole === 'admin' && editingOrdueviId) {
            fetchFacilities(editingOrdueviId);
        }
    }, [userRole, editingOrdueviId]);

    /* Sync: admin edits → user facilities (globalFacilitiesMap değiştiğinde kullanıcı tarafını güncelle) */
    useEffect(() => {
        if (globalOrduevi && globalFacilitiesMap[globalOrduevi.id]) {
            setFacilities(globalFacilitiesMap[globalOrduevi.id]);
        }
    }, [globalFacilitiesMap, globalOrduevi]);

    useEffect(() => {
        if (currentView !== 'home' && currentView !== 'profile' && currentView !== null) fetchSlots(currentView);
    }, [currentView]);

    /* ─── API Calls ─── */
    const fetchFacilities = async (ordueviId) => {
        try {
            const res = await axios.get(`${API}/facilities/${ordueviId}`);
            const raw = Array.isArray(res.data) ? res.data : [];
            const facs = raw.map(f => {
                const info = getFacilityInfo(f.name || '');
                return {
                    id: f.id,
                    name: f.name,
                    isAppointmentBased: f.isAppointmentBased,
                    type: f.isAppointmentBased ? 'appointment' : 'walk-in',
                    image: info.image,
                    desc: info.desc,
                    services: info.services,
                    staff: [],
                    hours: {
                        weekdayOpen: safeTime(f.openingTime, '08:00'),
                        weekdayClose: safeTime(f.closingTime, '17:00')
                    }
                };
            });
            setGlobalFacilitiesMap(prev => ({ ...prev, [ordueviId]: facs }));
            setFacilities(facs);
        } catch (err) {
            console.error('Tesisler yüklenirken hata:', err);
            // Hata durumunda en azından eski demo verileriyle devam et
            let facs = globalFacilitiesMap[ordueviId];
            if (!facs) {
                facs = generateDefaultFacilities(ordueviId);
                setGlobalFacilitiesMap(prev => ({ ...prev, [ordueviId]: facs }));
            }
            setFacilities(facs);
        }
    };

    const fetchSlots = async (facilityId) => {
        setLoading(true);
        try {
            const res = await axios.get(`${API}/slots/${facilityId}/${todayDate}T00:00:00.000Z`);
            setSlots(res.data);
        } catch (err) {
            console.error('Slotlar yüklenirken hata:', err);
            const mockSlots = [];
            let h = 8, m = 0;
            for (let i = 0; i < 36; i++) {
                let s = new Date(); s.setHours(h, m, 0, 0);
                m += 15; if (m >= 60) { h++; m = 0; }
                let e = new Date(); e.setHours(h, m, 0, 0);
                mockSlots.push({ startTime: s.toISOString(), endTime: e.toISOString(), isAvailable: Math.random() > 0.4, occupiedByUserId: Math.random() > 0.8 ? USER_ID : null });
            }
            setSlots(mockSlots);
        } finally {
            setLoading(false);
        }
    };

    const handleSlotClick = async (slot) => {
        if (!slot.isAvailable) return;
        setActionLoading(true);
        try {
            const res = await axios.post(`${API}/lock`, { facilityId: currentView, startTime: slot.startTime, endTime: slot.endTime, userId: USER_ID });
            if (res.status === 200) { setSelectedSlot(slot); setIsModalOpen(true); fetchSlots(currentView); }
        } catch (err) {
            setTimeout(() => { setSelectedSlot(slot); setIsModalOpen(true); setActionLoading(false); }, 400);
        }
    };

    const handleConfirmReservation = async () => {
        if (!selectedSlot) return;
        setActionLoading(true);
        const newRes = {
            id: Date.now().toString(),
            facilityId: currentView,
            facilityName: currentFacilityObj?.name || currentView,
            ordueviName: globalOrduevi?.name || '',
            startTime: selectedSlot.startTime,
            endTime: selectedSlot.endTime,
            date: new Date().toLocaleDateString('tr-TR'),
            status: 'confirmed'
        };
        try {
            const res = await axios.post(`${API}/confirm`, { facilityId: currentView, startTime: selectedSlot.startTime, userId: USER_ID });
            if (res.status === 200) { setMyReservations(prev => [...prev, newRes]); alert("Randevunuz Başarıyla Oluşturuldu!"); setIsModalOpen(false); fetchSlots(currentView); }
        } catch (err) {
            setTimeout(() => { setMyReservations(prev => [...prev, newRes]); alert("Randevunuz Başarıyla Oluşturuldu!"); setIsModalOpen(false); fetchSlots(currentView); setActionLoading(false); }, 500);
        }
    };

    const formatTime = (iso) => new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

    const handleAddOrduevi = async () => {
        if (!newOrdueviName || !newOrdueviLoc) return;
        try {
            const res = await axios.post(ORDUEVI_API, {
                name: newOrdueviName,
                location: newOrdueviLoc,
                description: newOrdueviDesc,
                contactNumber: newOrdueviContact
            });
            const created = res.data;
            setAllOrduevis([...allOrduevis, created]);
            setNewOrdueviName('');
            setNewOrdueviLoc('');
            setNewOrdueviDesc('');
            setNewOrdueviContact('');
            alert("Orduevi başarıyla sisteme eklendi!");
        } catch (err) {
            console.error('Orduevi eklenirken hata:', err);
            alert('Orduevi eklenirken bir hata oluştu. Lütfen tekrar deneyin.');
        }
    };

    const handleDeleteOrduevi = async (id) => {
        if (!window.confirm("Bu orduevini sistemden silmek istediğinize emin misiniz?")) return;
        try {
            await axios.delete(`${ORDUEVI_API}/${id}`);
            setAllOrduevis(allOrduevis.filter(o => o.id !== id));
            if (globalOrduevi?.id === id) {
                setGlobalOrduevi(null);
                setFacilities([]);
            }
        } catch (err) {
            console.error('Orduevi silinirken hata:', err);
            alert('Orduevi silinirken bir hata oluştu. Lütfen tekrar deneyin.');
        }
    };

    /* ─── Image Upload Handler ─── */
    const handleImageUpload = (e) => {
        const file = e.target.files?.[0];
        if (!file) return;
        if (!file.type.startsWith('image/')) { alert('Lütfen geçerli bir görsel dosyası seçin.'); return; }
        if (file.size > 5 * 1024 * 1024) { alert('Dosya boyutu 5MB\'dan küçük olmalıdır.'); return; }
        const reader = new FileReader();
        reader.onload = (ev) => {
            setEditingFacility(prev => ({ ...prev, image: ev.target.result }));
        };
        reader.readAsDataURL(file);
    };

    /* ─── Cancel Reservation Handler ─── */
    const handleCancelReservation = (resId) => {
        if (window.confirm('Bu randevuyu iptal etmek istediğinize emin misiniz?')) {
            setMyReservations(prev => prev.filter(r => r.id !== resId));
            alert('Randevunuz başarıyla iptal edildi.');
        }
    };

    const filteredOrduevis = allOrduevis.filter(o =>
        o.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
        o.location.toLowerCase().includes(searchQuery.toLowerCase())
    );

    const currentFacilityObj = facilities.find(f => f.id === currentView);
    const updateRegisterField = (field, value) => setRegisterForm(prev => ({ ...prev, [field]: value }));

    /* ─── Login/Register ─── */
    const handleLogin = async (role) => {
        if (role === 'admin') { setUserRole('admin'); setIsAuthenticated(true); return; }
        if (!loginIdentityNo || !loginPassword) { alert('Lütfen T.C. kimlik no ve şifre giriniz.'); return; }
        try {
            const res = await axios.post(`${AUTH_API}/login`, { identityNumber: loginIdentityNo, password: loginPassword });
            setLoggedInUser(res.data); setUserRole('user'); setIsAuthenticated(true);
        } catch (error) {
            const status = error?.response?.status;
            const isApiDown = !error?.response || status === 502 || status === 503 || status === 504 || error?.code === 'ECONNREFUSED' || error?.code === 'ERR_NETWORK';
            if (isApiDown) { setLoggedInUser({ firstName: 'Demo', lastName: 'Kullanıcı', identityNumber: loginIdentityNo }); setUserRole('user'); setIsAuthenticated(true); return; }
            alert(error?.response?.data?.Message || 'Giriş başarısız. Bilgilerinizi kontrol ediniz.');
        }
    };

    const handleRegister = async () => {
        if (!registerForm.identityNumber || !registerForm.password || !registerForm.firstName || !registerForm.lastName) { alert('T.C. kimlik no, ad, soyad ve şifre alanları zorunludur.'); return; }
        if (registerForm.password.length < 6) { alert('Şifre en az 6 karakter olmalıdır.'); return; }
        if (isRelative && (!registerForm.ownerTcNo || !registerForm.ownerFirstName || !registerForm.ownerLastName || !registerForm.ownerRank)) { alert('Yakın olarak kayıt yapıyorsanız asıl hak sahibinin tüm bilgilerini doldurmalısınız.'); return; }
        setRegisterLoading(true);
        try {
            await axios.post(`${AUTH_API}/register`, { ...registerForm, relation: isRelative ? registerForm.relation : 'Kendisi' });
            alert('Kayıt başarıyla tamamlandı! Giriş yapabilirsiniz.');
            setAuthMode('login'); setLoginIdentityNo(registerForm.identityNumber); setLoginPassword('');
        } catch (error) {
            const status = error?.response?.status;
            const isApiDown = !error?.response || status === 502 || status === 503 || status === 504 || error?.code === 'ECONNREFUSED' || error?.code === 'ERR_NETWORK';
            if (isApiDown) { alert('Kayıt başarıyla tamamlandı! (Demo mod) Giriş yapabilirsiniz.'); setAuthMode('login'); setLoginIdentityNo(registerForm.identityNumber); return; }
            alert(error?.response?.data?.Message || 'Kayıt sırasında hata oluştu.');
        } finally { setRegisterLoading(false); }
    };

    const handleLogout = () => { setIsAuthenticated(false); setUserRole(null); setLoggedInUser(null); setGlobalOrduevi(null); };

    /* ═══════════════════════════════════════════════════════════════════
       RENDER — LOGIN / REGISTER
       ═══════════════════════════════════════════════════════════════════ */
    if (!isAuthenticated) {
        return (
            <div className="login-wrapper fade-in">
                <div className="login-overlay">
                    <div className="login-box">
                        {/* Logo + Title */}
                        <img src="/logo.png" alt="TSK" className="login-logo" />
                        <h2>ORDUCEP</h2>
                        <p className="login-subtitle">TSK Orduevi ve Sosyal Tesisler Yönetim Sistemi</p>

                        {authMode === 'login' ? (
                            <div className="login-form">
                                <input type="text" className="login-input" placeholder="T.C. Kimlik No" value={loginIdentityNo} onChange={e => setLoginIdentityNo(e.target.value)} />
                                <input type="password" className="login-input" placeholder="Şifre" value={loginPassword} onChange={e => setLoginPassword(e.target.value)} />

                                <button className="btn btn-primary" style={{ width: '100%', padding: '.9rem' }} onClick={() => handleLogin('user')}>
                                    Giriş Yap
                                </button>

                                <div style={{ display: 'flex', gap: '.75rem' }}>
                                    <button className="btn btn-outline" style={{ flex: 1 }} onClick={() => handleLogin('admin')}>
                                        Admin Girişi
                                    </button>
                                    <button className="btn btn-outline" style={{ flex: 1 }} onClick={() => setAuthMode('register')}>
                                        Kayıt Ol
                                    </button>
                                </div>
                            </div>
                        ) : (
                            <div className="login-form" style={{ maxHeight: '68vh', overflowY: 'auto', paddingRight: '.25rem' }}>
                                <div className="register-section-title">Kişisel Bilgiler</div>
                                <input type="text" className="login-input" placeholder="Ad *" value={registerForm.firstName} onChange={e => updateRegisterField('firstName', e.target.value)} />
                                <input type="text" className="login-input" placeholder="Soyad *" value={registerForm.lastName} onChange={e => updateRegisterField('lastName', e.target.value)} />
                                <input type="text" className="login-input" placeholder="T.C. Kimlik Numarası *" maxLength={11} value={registerForm.identityNumber} onChange={e => updateRegisterField('identityNumber', e.target.value.replace(/\D/g, ''))} />

                                <div className="register-section-title">Hak Sahipliği</div>
                                <div style={{ display: 'flex', gap: '.6rem' }}>
                                    <button className={`btn ${!isRelative ? 'btn-primary' : 'btn-outline'}`} style={{ flex: 1, fontSize: '.84rem' }} onClick={() => { setIsRelative(false); updateRegisterField('relation', 'Kendisi'); }}>Asıl Hak Sahibiyim</button>
                                    <button className={`btn ${isRelative ? 'btn-primary' : 'btn-outline'}`} style={{ flex: 1, fontSize: '.84rem' }} onClick={() => { setIsRelative(true); updateRegisterField('relation', ''); }}>Yakınıyım</button>
                                </div>

                                {isRelative && (
                                    <>
                                        <select className="login-input" value={registerForm.relation} onChange={e => updateRegisterField('relation', e.target.value)}>
                                            <option value="">-- Yakınlık Seçin --</option>
                                            <option value="Eşi">Eşi</option>
                                            <option value="Oğlu">Oğlu</option>
                                            <option value="Kızı">Kızı</option>
                                            <option value="Annesi">Annesi</option>
                                            <option value="Babası">Babası</option>
                                        </select>
                                        <div className="register-section-title">Asıl Hak Sahibi Bilgileri</div>
                                        <input type="text" className="login-input" placeholder="Hak Sahibi T.C. *" maxLength={11} value={registerForm.ownerTcNo} onChange={e => updateRegisterField('ownerTcNo', e.target.value.replace(/\D/g, ''))} />
                                        <input type="text" className="login-input" placeholder="Hak Sahibi Ad *" value={registerForm.ownerFirstName} onChange={e => updateRegisterField('ownerFirstName', e.target.value)} />
                                        <input type="text" className="login-input" placeholder="Hak Sahibi Soyad *" value={registerForm.ownerLastName} onChange={e => updateRegisterField('ownerLastName', e.target.value)} />
                                        <input type="text" className="login-input" placeholder="Hak Sahibi Rütbe *" value={registerForm.ownerRank} onChange={e => updateRegisterField('ownerRank', e.target.value)} />
                                    </>
                                )}

                                <div className="register-section-title">Şifre</div>
                                <input type="password" className="login-input" placeholder="Şifre (en az 6 karakter) *" value={registerForm.password} onChange={e => updateRegisterField('password', e.target.value)} />

                                <div style={{ display: 'flex', gap: '.6rem', marginTop: '.5rem' }}>
                                    <button className="btn btn-primary" style={{ flex: 1 }} onClick={handleRegister} disabled={registerLoading}>
                                        {registerLoading ? 'Kaydediliyor...' : 'Kaydı Tamamla'}
                                    </button>
                                    <button className="btn btn-outline" style={{ flex: 1 }} onClick={() => setAuthMode('login')}>Girişe Dön</button>
                                </div>
                            </div>
                        )}


                    </div>
                </div>
            </div>
        );
    }

    /* ═══════════════════════════════════════════════════════════════════
       RENDER — ADMIN PANEL
       ═══════════════════════════════════════════════════════════════════ */
    if (userRole === 'admin') {
        /* Admin: Tesis Detay Yönetimi */
        if (editingOrdueviId) {
            const currentEditingObj = allOrduevis.find(o => o.id === editingOrdueviId);
            const currentAdminFacs = globalFacilitiesMap[editingOrdueviId] || generateDefaultFacilities(editingOrdueviId);
            return (
                <div style={{ minHeight: '100vh', background: 'var(--bg-primary)', position: 'relative', zIndex: 1 }}>
                    {/* Top Bar */}
                    <header style={{ padding: '1.25rem 2rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderBottom: '1px solid var(--border)', background: 'rgba(6,10,19,.8)', backdropFilter: 'blur(20px)', position: 'sticky', top: 0, zIndex: 50 }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                            <img src="/logo.png" alt="" style={{ width: '40px', opacity: .8 }} />
                            <div>
                                <div style={{ fontSize: '.7rem', color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '2px', marginBottom: '.15rem' }}>Tesis Yönetimi</div>
                                <div style={{ color: 'var(--gold)', fontWeight: 700, fontSize: '1.05rem' }}>{currentEditingObj?.name}</div>
                            </div>
                        </div>
                        <button className="btn btn-outline" onClick={() => setEditingOrdueviId(null)}>← Geri Dön</button>
                    </header>

                    <div style={{ maxWidth: '1000px', margin: '0 auto', padding: '2rem' }}>
                        {/* Add Facility Card */}
                        <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 'var(--radius-lg)', padding: '2rem', marginBottom: '1.5rem', position: 'relative', overflow: 'hidden' }}>
                            <div style={{ position: 'absolute', top: 0, left: 0, right: 0, height: '2px', background: 'var(--gold-gradient)', opacity: .25 }} />
                            <h3 style={{ color: 'var(--gold)', fontSize: '1rem', fontWeight: 700, marginBottom: '1.25rem', letterSpacing: '.5px' }}>Yeni Hizmet Birimi Ekle</h3>
                            <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr 1fr 1fr auto', gap: '.75rem', alignItems: 'end' }}>
                                <div>
                                    <label style={{ display: 'block', fontSize: '.75rem', color: 'var(--text-muted)', marginBottom: '.3rem', textTransform: 'uppercase', letterSpacing: '1px' }}>Birim Adı</label>
                                    <input className="login-input" placeholder="Örn: Erkek Kuaförü" value={newFacilityName} onChange={e => setNewFacilityName(e.target.value)} />
                                </div>
                                <div>
                                    <label style={{ display: 'block', fontSize: '.75rem', color: 'var(--text-muted)', marginBottom: '.3rem', textTransform: 'uppercase', letterSpacing: '1px' }}>Tip</label>
                                    <select className="login-input" value={newFacilityIsAppointment} onChange={e => setNewFacilityIsAppointment(e.target.value)}>
                                        <option value="true">Randevulu</option>
                                        <option value="false">Randevusuz</option>
                                    </select>
                                </div>
                                <div>
                                    <label style={{ display: 'block', fontSize: '.75rem', color: 'var(--text-muted)', marginBottom: '.3rem', textTransform: 'uppercase', letterSpacing: '1px' }}>Açılış</label>
                                    <input className="login-input" placeholder="08:00" value={newFacilityOpen} onChange={e => setNewFacilityOpen(e.target.value)} />
                                </div>
                                <div>
                                    <label style={{ display: 'block', fontSize: '.75rem', color: 'var(--text-muted)', marginBottom: '.3rem', textTransform: 'uppercase', letterSpacing: '1px' }}>Kapanış</label>
                                    <input className="login-input" placeholder="17:00" value={newFacilityClose} onChange={e => setNewFacilityClose(e.target.value)} />
                                </div>
                                <button className="btn btn-primary" onClick={handleAddFacility}>Ekle</button>
                            </div>
                        </div>

                        {/* Existing Facilities List */}
                        <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 'var(--radius-lg)', padding: '2rem', position: 'relative', overflow: 'hidden' }}>
                            <div style={{ position: 'absolute', top: 0, left: 0, right: 0, height: '2px', background: 'var(--gold-gradient)', opacity: .25 }} />
                            <h3 style={{ color: 'var(--gold)', fontSize: '1rem', fontWeight: 700, marginBottom: '1.5rem' }}>Mevcut Hizmet Birimleri ({currentAdminFacs.length})</h3>
                            <div style={{ display: 'flex', flexDirection: 'column', gap: '.5rem' }}>
                                {currentAdminFacs.map((fac, i) => (
                                    <div key={fac.id} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '1rem 1.25rem', background: 'var(--bg-input)', borderRadius: 'var(--radius-sm)', border: '1px solid var(--border)', transition: 'all .2s' }}>
                                        <div>
                                            <div style={{ fontWeight: 600, marginBottom: '.2rem' }}>{i + 1}. {fac.name}</div>
                                            <div style={{ fontSize: '.82rem', color: 'var(--text-muted)' }}>
                                                {fac.hours?.weekdayOpen || '-'} – {fac.hours?.weekdayClose || '-'} &nbsp;·&nbsp;
                                                <span style={{ color: fac.type === 'appointment' ? 'var(--success)' : 'var(--warning)' }}>{fac.type === 'appointment' ? 'Randevulu' : 'Randevusuz'}</span>
                                            </div>
                                        </div>
                                        <div style={{ display: 'flex', gap: '.4rem' }}>
                                            <button className="btn btn-outline" style={{ padding: '.4rem .8rem', fontSize: '.82rem' }} onClick={() => setEditingFacility(JSON.parse(JSON.stringify(fac)))}>Düzenle</button>
                                            <button
                                                className="btn btn-outline"
                                                style={{ padding: '.4rem .8rem', fontSize: '.82rem', color: 'var(--danger)', borderColor: 'rgba(239,68,68,.2)' }}
                                                onClick={async () => {
                                                    if (!window.confirm('Bu hizmet birimini silmek istediğinize emin misiniz?')) return;
                                                    try {
                                                        await axios.delete(`${API}/facilities/${fac.id}`);
                                                        const facs = (globalFacilitiesMap[editingOrdueviId] || currentAdminFacs).filter(f => f.id !== fac.id);
                                                        setGlobalFacilitiesMap(prev => ({ ...prev, [editingOrdueviId]: facs }));
                                                        alert('Hizmet birimi başarıyla silindi.');
                                                    } catch (err) {
                                                        console.error('Tesis silinirken hata:', err);
                                                        alert('Tesis silinirken bir hata oluştu. Lütfen tekrar deneyin.');
                                                    }
                                                }}
                                            >
                                                Sil
                                            </button>
                                        </div>
                                    </div>
                                ))}
                            </div>
                        </div>
                    </div>

                    {/* Facility Edit Modal */}
                    {editingFacility && (
                        <div className="modal-overlay" onClick={() => setEditingFacility(null)}>
                            <div className="modal-content custom-scroll" onClick={e => e.stopPropagation()} style={{ maxWidth: '1000px', width: '95%', maxHeight: '90vh', overflowY: 'auto' }}>
                                <div style={{ borderBottom: '1px solid var(--border)', paddingBottom: '1rem', marginBottom: '2rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                                    <div>
                                        <h2 style={{ fontSize: '1.3rem' }}>{editingFacility.name}</h2>
                                        <p style={{ margin: 0, fontSize: '.88rem' }}>Tesisin kurallarını ve fiyatlandırmasını yapılandırın.</p>
                                    </div>
                                    <button className="btn btn-outline" onClick={() => setEditingFacility(null)} style={{ minWidth: 'auto', padding: '.4rem .8rem' }}>✕</button>
                                </div>

                                <div className="admin-modal-grid">
                                    {/* Left: Visual & Staff */}
                                    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
                                        <div className="admin-setting-section">
                                            <div className="admin-setting-title">Vitrin & Açıklama</div>
                                            <div style={{ display: 'flex', gap: '1rem', alignItems: 'flex-start' }}>
                                                <div style={{ flex: 1 }}>
                                                    <label style={{ color: 'var(--text-muted)', fontSize: '.78rem', textTransform: 'uppercase', letterSpacing: '1px' }}>Görsel</label>
                                                    <div className="upload-area" style={{ marginTop: '.35rem' }}>
                                                        <input type="file" accept="image/*" id="facility-image-upload" style={{ display: 'none' }} onChange={handleImageUpload} />
                                                        <label htmlFor="facility-image-upload" className="upload-btn">
                                                            <span className="upload-icon">📷</span>
                                                            <span>Fotoğraf Yükle</span>
                                                        </label>
                                                        {editingFacility.image && (
                                                            <button className="btn btn-outline" style={{ padding: '.3rem .6rem', fontSize: '.72rem', color: 'var(--danger)', borderColor: 'rgba(239,68,68,.2)', marginTop: '.4rem' }} onClick={() => setEditingFacility({ ...editingFacility, image: '' })}>Görseli Kaldır</button>
                                                        )}
                                                    </div>
                                                </div>
                                                {editingFacility.image && (
                                                    <div className="upload-preview" style={{ width: '100px', height: '100px', borderRadius: 'var(--radius-sm)', background: `url(${editingFacility.image}) center/cover`, border: '2px solid var(--border-strong)', flexShrink: 0, boxShadow: 'var(--shadow-md)' }} />
                                                )}
                                            </div>
                                            <label style={{ color: 'var(--text-muted)', fontSize: '.78rem', marginTop: '1rem', display: 'block', textTransform: 'uppercase', letterSpacing: '1px' }}>Tanıtım Metni</label>
                                            <textarea className="login-input" style={{ resize: 'none', height: '90px', marginTop: '.2rem' }} value={editingFacility.desc || ''} onChange={e => setEditingFacility({ ...editingFacility, desc: e.target.value })} placeholder="Açıklama..." />
                                        </div>

                                        <div className="admin-setting-section">
                                            <div className="admin-setting-title">Personel ({editingFacility.staff?.length || 0})</div>
                                            {editingFacility.staff?.map((st, idx) => (
                                                <div key={idx} style={{ display: 'flex', gap: '.4rem', marginBottom: '.4rem' }}>
                                                    <input type="text" className="login-input" style={{ flex: 1, padding: '.7rem' }} value={st} onChange={e => { const s = [...editingFacility.staff]; s[idx] = e.target.value; setEditingFacility({ ...editingFacility, staff: s }); }} placeholder="Rütbe / İsim" />
                                                    <button className="btn btn-outline" style={{ color: 'var(--danger)', borderColor: 'rgba(239,68,68,.2)', padding: '.4rem .6rem' }} onClick={() => setEditingFacility({ ...editingFacility, staff: editingFacility.staff.filter((_, i) => i !== idx) })}>✕</button>
                                                </div>
                                            ))}
                                            <button className="btn btn-outline" style={{ borderStyle: 'dashed', width: '100%', marginTop: '.4rem', color: 'var(--gold)', borderColor: 'var(--gold-dark)', opacity: .7 }} onClick={() => setEditingFacility({ ...editingFacility, staff: [...(editingFacility.staff || []), ''] })}>+ Personel Ekle</button>
                                        </div>
                                    </div>

                                    {/* Right: Hours & Pricing */}
                                    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
                                        <div className="admin-setting-section">
                                            <div className="admin-setting-title">Mesai Saatleri</div>
                                            <label style={{ color: 'var(--text-muted)', fontSize: '.78rem' }}>Hafta İçi</label>
                                            <div style={{ display: 'flex', gap: '.75rem', margin: '.3rem 0 1rem' }}>
                                                <input type="text" className="login-input" placeholder="08:00" value={editingFacility.hours?.weekdayOpen || ''} onChange={e => setEditingFacility({ ...editingFacility, hours: { ...editingFacility.hours, weekdayOpen: e.target.value } })} />
                                                <input type="text" className="login-input" placeholder="17:00" value={editingFacility.hours?.weekdayClose || ''} onChange={e => setEditingFacility({ ...editingFacility, hours: { ...editingFacility.hours, weekdayClose: e.target.value } })} />
                                            </div>
                                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                                                <label style={{ color: 'var(--text-muted)', fontSize: '.78rem' }}>Hafta Sonu</label>
                                                <label style={{ fontSize: '.76rem', color: 'var(--danger)', display: 'flex', alignItems: 'center', gap: '.3rem', cursor: 'pointer', fontWeight: 600 }}>
                                                    <input type="checkbox" checked={editingFacility.hours?.weekendClosed || false} onChange={e => setEditingFacility({ ...editingFacility, hours: { ...editingFacility.hours, weekendClosed: e.target.checked } })} />
                                                    Kapalı
                                                </label>
                                            </div>
                                            <div style={{ display: 'flex', gap: '.75rem', margin: '.3rem 0 1rem', opacity: editingFacility.hours?.weekendClosed ? .3 : 1, pointerEvents: editingFacility.hours?.weekendClosed ? 'none' : 'auto' }}>
                                                <input type="text" className="login-input" placeholder="Açılış" value={editingFacility.hours?.weekendOpen || ''} onChange={e => setEditingFacility({ ...editingFacility, hours: { ...editingFacility.hours, weekendOpen: e.target.value } })} />
                                                <input type="text" className="login-input" placeholder="Kapanış" value={editingFacility.hours?.weekendClose || ''} onChange={e => setEditingFacility({ ...editingFacility, hours: { ...editingFacility.hours, weekendClose: e.target.value } })} />
                                            </div>
                                            <label style={{ color: 'var(--text-muted)', fontSize: '.78rem' }}>Kapalı Günler</label>
                                            <div style={{ display: 'flex', gap: '.35rem', flexWrap: 'wrap', marginTop: '.3rem' }}>
                                                {['Pzt', 'Sal', 'Çar', 'Per', 'Cum', 'Cmt', 'Paz'].map(day => (
                                                    <label key={day} style={{ padding: '.25rem .55rem', borderRadius: 'var(--radius-xs)', border: `1px solid ${editingFacility.hours?.closedDays?.includes(day) ? 'var(--danger)' : 'var(--border)'}`, backgroundColor: editingFacility.hours?.closedDays?.includes(day) ? 'var(--danger-bg)' : 'var(--bg-input)', color: editingFacility.hours?.closedDays?.includes(day) ? '#fca5a5' : 'var(--text-muted)', cursor: 'pointer', fontSize: '.82rem', userSelect: 'none', transition: 'all .2s' }}>
                                                        <input type="checkbox" style={{ display: 'none' }} checked={editingFacility.hours?.closedDays?.includes(day) || false} onChange={e => { const cur = editingFacility.hours?.closedDays || []; setEditingFacility({ ...editingFacility, hours: { ...editingFacility.hours, closedDays: e.target.checked ? [...cur, day] : cur.filter(d => d !== day) } }); }} />
                                                        {day}
                                                    </label>
                                                ))}
                                            </div>
                                        </div>

                                        <div className="admin-setting-section">
                                            <div className="admin-setting-title">Hizmetler & Fiyatlar</div>
                                            {editingFacility.services?.map((srv, idx) => (
                                                <div key={idx} style={{ display: 'flex', gap: '.4rem', marginBottom: '.4rem' }}>
                                                    <input type="text" className="login-input" style={{ flex: 2, padding: '.7rem' }} value={srv.name} onChange={e => { const s = [...editingFacility.services]; s[idx] = { ...s[idx], name: e.target.value }; setEditingFacility({ ...editingFacility, services: s }); }} placeholder="Hizmet" />
                                                    <div style={{ position: 'relative', flex: 1 }}>
                                                        <input type="number" className="login-input" style={{ width: '100%', padding: '.7rem', paddingRight: '2rem' }} value={srv.price} onChange={e => { const s = [...editingFacility.services]; s[idx] = { ...s[idx], price: e.target.value }; setEditingFacility({ ...editingFacility, services: s }); }} placeholder="₺" />
                                                        <span style={{ position: 'absolute', right: '.6rem', top: '50%', transform: 'translateY(-50%)', color: 'var(--text-muted)', fontSize: '.75rem' }}>TL</span>
                                                    </div>
                                                    <button className="btn btn-outline" style={{ color: 'var(--danger)', borderColor: 'rgba(239,68,68,.2)', padding: '.4rem .6rem' }} onClick={() => setEditingFacility({ ...editingFacility, services: editingFacility.services.filter((_, i) => i !== idx) })}>✕</button>
                                                </div>
                                            ))}
                                            <button className="btn btn-outline" style={{ borderStyle: 'dashed', width: '100%', marginTop: '.4rem', color: 'var(--success)', borderColor: 'rgba(16,185,129,.3)', opacity: .8 }} onClick={() => setEditingFacility({ ...editingFacility, services: [...(editingFacility.services || []), { name: '', price: '' }] })}>+ Fiyat Satırı</button>
                                        </div>
                                    </div>
                                </div>

                                {/* Save / Cancel */}
                                <div style={{ display: 'flex', gap: '1rem', justifyContent: 'flex-end', marginTop: '2rem', paddingTop: '1.25rem', borderTop: '1px solid var(--border)' }}>
                                    <button className="btn btn-outline" onClick={() => setEditingFacility(null)}>İptal</button>
                                    <button className="btn btn-primary" style={{ minWidth: '200px' }} onClick={() => {
                                        alert("Tesis ayarları başarıyla güncellendi.");
                                        const facs = globalFacilitiesMap[editingOrdueviId] || generateDefaultFacilities(editingOrdueviId);
                                        setGlobalFacilitiesMap(prev => ({ ...prev, [editingOrdueviId]: facs.map(f => f.id === editingFacility.id ? editingFacility : f) }));
                                        setEditingFacility(null);
                                    }}>Kaydet</button>
                                </div>
                            </div>
                        </div>
                    )}
                </div>
            );
        }

        /* Admin: Ana Liste */
        return (
            <div style={{ minHeight: '100vh', background: 'var(--bg-primary)', position: 'relative', zIndex: 1 }}>
                <header style={{ padding: '1.25rem 2rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderBottom: '1px solid var(--border)', background: 'rgba(6,10,19,.8)', backdropFilter: 'blur(20px)', position: 'sticky', top: 0, zIndex: 50 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                        <img src="/logo.png" alt="" style={{ width: '40px', opacity: .8 }} />
                        <div>
                            <div style={{ fontSize: '.7rem', color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '2px' }}>Admin Panel</div>
                            <div style={{ color: 'var(--gold)', fontWeight: 700 }}>Süper Yönetici</div>
                        </div>
                    </div>
                    <button className="btn btn-outline" style={{ borderColor: 'rgba(239,68,68,.3)', color: 'var(--danger)' }} onClick={handleLogout}>Çıkış Yap</button>
                </header>

                <div style={{ maxWidth: '1000px', margin: '0 auto', padding: '2rem' }}>
                    {/* Search */}
                    <div style={{ marginBottom: '1.5rem' }}>
                        <input type="text" className="login-input" style={{ borderRadius: 'var(--radius-full)', padding: '1rem 1.5rem' }} placeholder="🔍 Orduevi ara..." value={adminSearchQuery} onChange={e => setAdminSearchQuery(e.target.value)} />
                    </div>

                    {/* Add Orduevi */}
                    <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 'var(--radius-lg)', padding: '2rem', marginBottom: '1.5rem', position: 'relative', overflow: 'hidden' }}>
                        <div style={{ position: 'absolute', top: 0, left: 0, right: 0, height: '2px', background: 'var(--gold-gradient)', opacity: .25 }} />
                        <h3 style={{ color: 'var(--gold)', fontSize: '1rem', fontWeight: 700, marginBottom: '1.25rem' }}>Yeni Orduevi Ekle</h3>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '.75rem' }}>
                            <div style={{ display: 'flex', gap: '.75rem' }}>
                                <div style={{ flex: 2 }}>
                                    <label style={{ display: 'block', fontSize: '.75rem', color: 'var(--text-muted)', marginBottom: '.3rem', textTransform: 'uppercase', letterSpacing: '1px' }}>Tesis Adı</label>
                                    <input className="login-input" placeholder="Örn: Sivas Orduevi" value={newOrdueviName} onChange={e => setNewOrdueviName(e.target.value)} />
                                </div>
                                <div style={{ flex: 1 }}>
                                    <label style={{ display: 'block', fontSize: '.75rem', color: 'var(--text-muted)', marginBottom: '.3rem', textTransform: 'uppercase', letterSpacing: '1px' }}>Konum</label>
                                    <input className="login-input" placeholder="Merkez / Sivas" value={newOrdueviLoc} onChange={e => setNewOrdueviLoc(e.target.value)} />
                                </div>
                            </div>
                            <div style={{ display: 'flex', gap: '.75rem', alignItems: 'flex-end' }}>
                                <div style={{ flex: 3 }}>
                                    <label style={{ display: 'block', fontSize: '.75rem', color: 'var(--text-muted)', marginBottom: '.3rem', textTransform: 'uppercase', letterSpacing: '1px' }}>Açıklama</label>
                                    <input className="login-input" placeholder="Kısa açıklama (opsiyonel)" value={newOrdueviDesc} onChange={e => setNewOrdueviDesc(e.target.value)} />
                                </div>
                                <div style={{ flex: 1 }}>
                                    <label style={{ display: 'block', fontSize: '.75rem', color: 'var(--text-muted)', marginBottom: '.3rem', textTransform: 'uppercase', letterSpacing: '1px' }}>İletişim No</label>
                                    <input className="login-input" placeholder="0xxx xxx xx xx" value={newOrdueviContact} onChange={e => setNewOrdueviContact(e.target.value)} />
                                </div>
                                <button className="btn btn-primary" onClick={handleAddOrduevi}>Ekle</button>
                            </div>
                        </div>
                    </div>

                    {/* Orduevi List */}
                    <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 'var(--radius-lg)', padding: '2rem', position: 'relative', overflow: 'hidden' }}>
                        <div style={{ position: 'absolute', top: 0, left: 0, right: 0, height: '2px', background: 'var(--gold-gradient)', opacity: .25 }} />
                        <h3 style={{ color: 'var(--gold)', fontSize: '1rem', fontWeight: 700, marginBottom: '1.5rem' }}>Kayıtlı Tesisler ({filteredAdminOrduevis.length})</h3>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '.5rem' }}>
                            {filteredAdminOrduevis.map(o => (
                                <div key={o.id} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '1rem 1.25rem', background: 'var(--bg-input)', borderRadius: 'var(--radius-sm)', border: '1px solid var(--border)', transition: 'all .2s' }}>
                                    <div>
                                        <div style={{ fontWeight: 600, color: 'var(--text-primary)', marginBottom: '.15rem' }}>{o.name}</div>
                                        <div style={{ fontSize: '.8rem', color: 'var(--text-muted)' }}>{o.location}</div>
                                    </div>
                                    <div style={{ display: 'flex', gap: '.5rem' }}>
                                        <button className="btn btn-primary" style={{ padding: '.4rem .8rem', fontSize: '.82rem' }} onClick={() => setEditingOrdueviId(o.id)}>Yönet</button>
                                        <button className="btn btn-outline" style={{ padding: '.4rem .8rem', fontSize: '.82rem', color: 'var(--danger)', borderColor: 'rgba(239,68,68,.2)' }} onClick={() => handleDeleteOrduevi(o.id)}>Sil</button>
                                    </div>
                                </div>
                            ))}
                        </div>
                    </div>
                </div>
            </div>
        );
    }

    /* ═══════════════════════════════════════════════════════════════════
       RENDER — ORDUEVİ SEÇİMİ
       ═══════════════════════════════════════════════════════════════════ */
    if (!globalOrduevi) {
        return (
            <div className="selection-wrapper fade-in">
                {/* Header */}
                <div style={{ textAlign: 'center', marginBottom: '2.5rem', animation: 'fadeInDown .8s ease-out' }}>
                    <img src="/logo.png" alt="TSK" style={{ width: '100px', height: 'auto', marginBottom: '1rem', filter: 'drop-shadow(0 4px 16px rgba(212,175,55,.15))' }} />
                    <h1 style={{ fontSize: '2rem', margin: 0, fontWeight: 900, letterSpacing: '3px', textTransform: 'uppercase' }}>
                        Tesis <span style={{ color: 'var(--gold)' }}>Seçimi</span>
                    </h1>
                    <p style={{ color: 'var(--text-muted)', fontSize: '.95rem', marginTop: '.5rem' }}>Hizmet almak istediğiniz orduevini seçiniz</p>
                </div>

                {/* Search */}
                <div className="search-container">
                    <input type="text" className="search-input" placeholder="🔍 Orduevi adı veya şehir ara..." value={searchQuery} onChange={e => setSearchQuery(e.target.value)} />
                </div>

                {/* Cards */}
                <div className="orduevi-cards-container">
                    {filteredOrduevis.map(o => (
                        <div key={o.id} className="orduevi-card" onClick={() => setGlobalOrduevi({ id: o.id, name: o.name })}>
                            <h3>{o.name}</h3>
                            <span>{o.location}</span>
                        </div>
                    ))}
                    {filteredOrduevis.length === 0 && (
                        <p style={{ textAlign: 'center', color: 'var(--text-muted)', width: '100%', gridColumn: '1/-1', padding: '3rem 0' }}>Arama kriterlerinize uygun tesis bulunamadı.</p>
                    )}
                </div>

                {/* Logout */}
                <div style={{ marginTop: '3rem' }}>
                    <button className="btn btn-outline" style={{ color: 'var(--danger)', borderColor: 'rgba(239,68,68,.2)' }} onClick={handleLogout}>Çıkış Yap</button>
                </div>
            </div>
        );
    }

    /* ═══════════════════════════════════════════════════════════════════
       RENDER — ANA UYGULAMA (Orduevi seçildi)
       ═══════════════════════════════════════════════════════════════════ */
    return (
        <div className="layout-container">
            {/* ─── Navbar ─── */}
            <nav className="navbar">
                <button className="hamburger-btn" onClick={() => setIsSidebarOpen(true)}>
                    <div className="bar" /><div className="bar" /><div className="bar" />
                </button>
                <div className="nav-title" onClick={() => setCurrentView('home')} style={{ cursor: 'pointer', flex: 1 }}>
                    {globalOrduevi?.name || 'OrduCep'}
                </div>
                <button className="btn btn-outline" style={{ padding: '.35rem .75rem', fontSize: '.78rem' }} onClick={() => setGlobalOrduevi(null)}>
                    Tesis Değiştir
                </button>
                {loggedInUser && (
                    <span className="nav-user-info" style={{ cursor: 'pointer' }} onClick={() => setCurrentView('profile')} title="Profilim">
                        👤 {loggedInUser.firstName} {loggedInUser.lastName}
                    </span>
                )}
            </nav>

            {/* ─── Sidebar Overlay ─── */}
            {isSidebarOpen && <div className="sidebar-overlay" onClick={() => setIsSidebarOpen(false)} />}

            {/* ─── Sidebar ─── */}
            <div className={`sidebar ${isSidebarOpen ? 'open' : ''}`}>
                <div className="sidebar-header">
                    <h2>Hizmetler</h2>
                    <button className="close-btn" onClick={() => setIsSidebarOpen(false)}>✕</button>
                </div>
                <ul className="sidebar-menu">
                    <li className={currentView === 'home' ? 'active' : ''} onClick={() => { setCurrentView('home'); setIsSidebarOpen(false); }}>
                        🏠 Ana Sayfa
                    </li>
                    {facilities.map(f => (
                        <li key={f.id} className={currentView === f.id ? 'active' : ''} onClick={() => { setCurrentView(f.id); setIsSidebarOpen(false); }}>
                            <span className="dot" /> {f.name}
                        </li>
                    ))}
                    <li className={currentView === 'profile' ? 'active' : ''} onClick={() => { setCurrentView('profile'); setIsSidebarOpen(false); }} style={{ marginTop: 'auto', borderTop: '1px solid var(--border)', paddingTop: '1rem' }}>
                        👤 Profilim
                    </li>
                    <li style={{ color: 'var(--warning)' }} onClick={() => { setGlobalOrduevi(null); setIsSidebarOpen(false); }}>
                        ↩ Tesis Değiştir
                    </li>
                    <li style={{ color: 'var(--danger)' }} onClick={handleLogout}>
                        Çıkış Yap
                    </li>
                </ul>
            </div>

            {/* ─── Main Content ─── */}
            <main className="main-content">
                {/* Home View */}
                {currentView === 'home' && (
                    <div className="home-view">
                        <div className="hero-section">
                            <img src="/orduevi.png" alt="Orduevi" className="hero-image" />
                            <div className="hero-overlay">
                                <h1>{globalOrduevi?.name}'ne Hoşgeldiniz</h1>
                                <p>Konfor, Güven ve Huzur Tek Çatı Altında</p>
                            </div>
                        </div>
                        <div className="home-content">
                            <h2>Hakkımızda</h2>
                            <p>
                                Orduevimiz; Türkiye'nin en seçkin sosyal tesisleri arasında yer almakta olup,
                                değerli askeri personelimize ve misafirlerimize en üst düzeyde hizmet sunmaktadır.
                                Yüksek standartları ve modern donanımıyla örnek teşkil eden tesislerimizde
                                sevdiklerinizle kaliteli vakit geçirebilirsiniz.
                            </p>
                            <p>
                                Sol üstteki menü ikonuna tıklayarak tesislerimizde yer alan <strong>Berber</strong>,
                                <strong> Kuaför</strong>, <strong> Restaurant</strong> ve diğer hizmet birimlerine
                                ulaşabilir, detayları inceleyebilir ve randevularınızı alabilirsiniz.
                            </p>
                        </div>
                    </div>
                )}

                {/* Profile View */}
                {currentView === 'profile' && (
                    <div className="profile-view fade-in">
                        <div className="profile-header-card">
                            <div className="profile-avatar">
                                <span>{loggedInUser?.firstName?.[0] || 'K'}{loggedInUser?.lastName?.[0] || 'U'}</span>
                            </div>
                            <div className="profile-header-info">
                                <h1>{loggedInUser?.firstName || 'Kullanıcı'} {loggedInUser?.lastName || ''}</h1>
                                <p className="profile-badge">
                                    {loggedInUser?.relation === 'Kendisi' ? '⭐ Hak Sahibi' : `👥 ${loggedInUser?.relation || 'Yakın'}`}
                                </p>
                            </div>
                        </div>

                        <div className="profile-grid">
                            {/* Personal Info Card */}
                            <div className="profile-card">
                                <div className="profile-card-header">
                                    <h3>👤 Kişisel Bilgiler</h3>
                                </div>
                                <div className="profile-info-list">
                                    <div className="profile-info-row">
                                        <span className="profile-info-label">Ad Soyad</span>
                                        <span className="profile-info-value">{loggedInUser?.firstName || '-'} {loggedInUser?.lastName || '-'}</span>
                                    </div>
                                    <div className="profile-info-row">
                                        <span className="profile-info-label">T.C. Kimlik No</span>
                                        <span className="profile-info-value">{loggedInUser?.identityNumber ? loggedInUser.identityNumber.slice(0, 3) + '****' + loggedInUser.identityNumber.slice(-2) : '***'}</span>
                                    </div>
                                    <div className="profile-info-row">
                                        <span className="profile-info-label">Hak Sahipliği</span>
                                        <span className="profile-info-value">{loggedInUser?.relation || 'Kendisi'}</span>
                                    </div>
                                    {loggedInUser?.relation !== 'Kendisi' && loggedInUser?.ownerFirstName && (
                                        <>
                                            <div className="profile-info-row">
                                                <span className="profile-info-label">Hak Sahibi</span>
                                                <span className="profile-info-value">{loggedInUser.ownerFirstName} {loggedInUser.ownerLastName || ''}</span>
                                            </div>
                                            <div className="profile-info-row">
                                                <span className="profile-info-label">Hak Sahibi Rütbe</span>
                                                <span className="profile-info-value">{loggedInUser.ownerRank || '-'}</span>
                                            </div>
                                        </>
                                    )}
                                </div>
                            </div>

                            {/* Reservations Card */}
                            <div className="profile-card">
                                <div className="profile-card-header">
                                    <h3>📅 Randevularım</h3>
                                    <span className="profile-res-count">{myReservations.length}</span>
                                </div>
                                {myReservations.length === 0 ? (
                                    <div className="profile-empty">
                                        <div className="profile-empty-icon">📋</div>
                                        <p>Henüz randevunuz bulunmamaktadır.</p>
                                        <p style={{ fontSize: '.82rem', color: 'var(--text-muted)' }}>Sol menüden bir hizmet birimi seçerek randevu alabilirsiniz.</p>
                                    </div>
                                ) : (
                                    <div className="profile-res-list">
                                        {myReservations.map(res => (
                                            <div key={res.id} className="profile-res-item">
                                                <div className="profile-res-info">
                                                    <div className="profile-res-name">{res.facilityName}</div>
                                                    <div className="profile-res-detail">
                                                        📍 {res.ordueviName} &nbsp;·&nbsp; 🕐 {formatTime(res.startTime)} &nbsp;·&nbsp; 📆 {res.date}
                                                    </div>
                                                </div>
                                                <div className="profile-res-actions">
                                                    <span className="profile-res-status confirmed">Onaylandı</span>
                                                    <button className="btn btn-outline profile-cancel-btn" onClick={() => handleCancelReservation(res.id)}>
                                                        İptal Et
                                                    </button>
                                                </div>
                                            </div>
                                        ))}
                                    </div>
                                )}
                            </div>
                        </div>
                    </div>
                )}

                {/* Facility View */}
                {currentView !== 'home' && currentView !== 'profile' && currentFacilityObj && (
                    <div className="facility-view">
                        <div className="facility-header">
                            <img src={currentFacilityObj.image || '/restaurant.png'} alt={currentFacilityObj.name} className="facility-banner" />
                            <div className="facility-title-box">
                                <h2>{currentFacilityObj.name}</h2>
                                <p>{currentFacilityObj.desc || ''}</p>
                            </div>
                        </div>

                        <div className="facility-body">
                            {/* Services */}
                            <div className="facility-services">
                                <h3>Hizmet & Fiyat</h3>
                                <ul className="service-list">
                                    {(currentFacilityObj.services || []).map((s, i) => (
                                        <li key={i}>
                                            <span className="service-name">{s.name}</span>
                                            <span className="service-price">{s.price} TL</span>
                                        </li>
                                    ))}
                                </ul>
                            </div>

                            {/* Booking / Walk-in */}
                            <div className="facility-booking">
                                {currentFacilityObj.isAppointmentBased ? (
                                    <>
                                        <h3>Randevu Saatleri</h3>
                                        {loading ? (
                                            <div className="loader-container">
                                                <div className="spinner" />
                                                <p>Saatler yükleniyor...</p>
                                            </div>
                                        ) : (
                                            <div className="slots-grid">
                                                {slots.map((slot, i) => {
                                                    let cssClass = 'booked', statusText = 'Dolu', statusClass = 'text-danger';
                                                    if (slot.isAvailable) { statusText = 'Müsait'; statusClass = 'text-success'; cssClass = 'available'; }
                                                    else if (slot.occupiedByUserId === USER_ID) { statusText = 'Sizin'; statusClass = 'text-warning'; cssClass = 'locked'; }
                                                    return (
                                                        <div key={i} className={`slot-card ${cssClass}`} onClick={() => handleSlotClick(slot)}>
                                                            <div className="slot-time">{formatTime(slot.startTime)}</div>
                                                            <div className={`slot-status ${statusClass}`}>{statusText}</div>
                                                        </div>
                                                    );
                                                })}
                                                {slots.length === 0 && <p className="text-muted">Uygun slot bulunamadı.</p>}
                                            </div>
                                        )}
                                    </>
                                ) : (
                                    <div style={{ textAlign: 'center', padding: '3rem 1.5rem', color: 'var(--text-secondary)' }}>
                                        <div style={{ fontSize: '2.5rem', marginBottom: '1rem', opacity: .6 }}>🚶</div>
                                        <h3 style={{ border: 'none', justifyContent: 'center', marginBottom: '.5rem', color: 'var(--text-primary)', fontWeight: 700 }}>Randevusuz Hizmet</h3>
                                        <p style={{ maxWidth: '400px', margin: '0 auto', lineHeight: 1.7 }}>Bu tesis geliş sırasına göre hizmet vermektedir. Çalışma saatleri içerisinde ziyaret edebilirsiniz.</p>
                                    </div>
                                )}
                            </div>
                        </div>
                    </div>
                )}
            </main>

            {/* ─── Reservation Modal ─── */}
            {isModalOpen && selectedSlot && (
                <div className="modal-overlay">
                    <div className="modal-content fade-in">
                        <h2>Randevuyu Onayla</h2>
                        <p>
                            <strong>{currentFacilityObj?.name}</strong> için <br />
                            <strong>{formatTime(selectedSlot.startTime)}</strong> saatinde 5 dakikalık kilit oluşturdunuz.
                            Onaylamak için aşağıdaki butona basınız.
                        </p>
                        <div className="btn-group">
                            <button className="btn btn-outline" onClick={() => setIsModalOpen(false)} disabled={actionLoading}>Vazgeç</button>
                            <button className="btn btn-primary" onClick={handleConfirmReservation} disabled={actionLoading}>
                                {actionLoading ? 'Onaylanıyor...' : 'Kesinleştir'}
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
